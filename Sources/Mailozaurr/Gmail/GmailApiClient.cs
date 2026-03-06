using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MimeKit;

namespace Mailozaurr;

/// <summary>
/// Lightweight client for sending and retrieving messages using Gmail REST API.
/// </summary>
public sealed class GmailApiClient : IDisposable {
    private readonly HttpClient _client;
    private readonly Func<CancellationToken, Task<string>>? _refreshToken;
    private readonly OAuthCredential? _credential;
    private bool _disposed;

    private void ThrowIfDisposed() {
        if (_disposed) {
            throw new ObjectDisposedException(nameof(GmailApiClient));
        }
    }

    /// <summary>
    /// Initializes the client using the provided OAuth credential.
    /// </summary>
    public GmailApiClient(OAuthCredential credential, Func<CancellationToken, Task<string>>? refreshToken = null) {
        if (credential == null) {
            throw new ArgumentNullException(nameof(credential));
        }

        _credential = credential;
        _client = new HttpClient {
            BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/")
        };
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
        _refreshToken = refreshToken;
    }

    /// <summary>
    /// Initializes the client using an externally managed <see cref="HttpClient"/>.
    /// </summary>
    /// <remarks>
    /// If <paramref name="client"/> does not specify <see cref="HttpClient.BaseAddress"/>, it will be set to the Gmail v1 endpoint
    /// (or <paramref name="baseAddress"/> if provided).
    /// </remarks>
    /// <param name="client">HTTP client to use for requests.</param>
    /// <param name="refreshToken">Optional delegate used to refresh an access token when a request returns 401/403.</param>
    /// <param name="credential">Optional OAuth credential holding an access token.</param>
    /// <param name="baseAddress">Optional Gmail base address used when <paramref name="client"/> has no base address configured.</param>
    public GmailApiClient(
        HttpClient client,
        Func<CancellationToken, Task<string>>? refreshToken = null,
        OAuthCredential? credential = null,
        Uri? baseAddress = null) {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _refreshToken = refreshToken;
        _credential = credential;
        if (_client.BaseAddress == null) {
            _client.BaseAddress = baseAddress ?? new Uri("https://gmail.googleapis.com/gmail/v1/");
        }
        if (credential != null && !string.IsNullOrEmpty(credential.AccessToken) && _client.DefaultRequestHeaders.Authorization == null) {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
        }
    }

    /// <summary>
    /// Repository used to persist messages that require retrying.
    /// </summary>
    public IPendingMessageRepository? PendingMessageRepository { get; set; }

    /// <summary>
    /// When set, sending is simulated and no Gmail request is issued.
    /// </summary>
    public bool DryRun { get; set; }

    /// <inheritdoc />
    public void Dispose() {
        if (_disposed) {
            return;
        }

        _client.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task ThrowIfAuthErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken) {
        if (response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden) {
            if (_refreshToken != null) {
                string token = await _refreshToken(cancellationToken).ConfigureAwait(false);
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (_credential != null) {
                    _credential.AccessToken = token;
                }
            }
#if NET5_0_OR_GREATER
            string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            throw new GmailAuthenticationException(response.StatusCode, content);
        }
    }

    private string? ResolveAccessToken() {
        var token = _credential?.AccessToken;
        if (!string.IsNullOrEmpty(token)) {
            return token;
        }

        var authorization = _client.DefaultRequestHeaders.Authorization;
        if (authorization != null && authorization.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)) {
            return authorization.Parameter;
        }

        return null;
    }

    private async Task QueuePendingMessageAsync(string userId, MimeMessage message, CancellationToken cancellationToken) {
        if (PendingMessageRepository == null) {
            return;
        }

        var accessToken = ResolveAccessToken();
        if (string.IsNullOrEmpty(accessToken)) {
            return;
        }

        if (string.IsNullOrEmpty(message.MessageId)) {
            message.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId();
        }

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var record = new PendingMessageRecord {
            MessageId = message.MessageId!,
            MimeMessage = Convert.ToBase64String(stream.ToArray()),
            Timestamp = now,
            NextAttemptAt = now,
            Provider = EmailProvider.Gmail
        };

        var credential = _credential;
        var userName = !string.IsNullOrWhiteSpace(credential?.UserName) ? credential!.UserName : userId;
        var expiresOn = credential?.ExpiresOn ?? DateTimeOffset.MaxValue;
        record.ProviderData[GmailPendingMessageSender.UserIdKey] = userId;
        record.ProviderData[GmailPendingMessageSender.UserNameKey] = userName;
        record.ProviderData[GmailPendingMessageSender.ExpiresOnKey] = expiresOn.ToString("o", CultureInfo.InvariantCulture);

        var protector = CredentialProtection.Default;
        record.ProviderData[GmailPendingMessageSender.AccessTokenProtectedKey] = protector.Protect(accessToken!);
        record.ProviderData.Remove(GmailPendingMessageSender.AccessTokenKey);
        record.ProviderData.Remove(GmailPendingMessageSender.AccessTokenBase64Key);
        var refresh = credential?.RefreshToken;
        if (!string.IsNullOrEmpty(refresh)) {
            record.ProviderData[GmailPendingMessageSender.RefreshTokenProtectedKey] = protector.Protect(refresh!);
        }
        record.ProviderData.Remove(GmailPendingMessageSender.RefreshTokenKey);
        record.ProviderData.Remove(GmailPendingMessageSender.RefreshTokenBase64Key);

        var clientId = credential?.ClientId;
        if (!string.IsNullOrEmpty(clientId)) {
            record.ProviderData[GmailPendingMessageSender.ClientIdKey] = clientId!;
        }

        var clientSecret = credential?.ClientSecret;
        if (!string.IsNullOrEmpty(clientSecret)) {
            record.ProviderData[GmailPendingMessageSender.ClientSecretProtectedKey] = protector.Protect(clientSecret!);
        }

        var serviceJson = credential?.ServiceAccountJson;
        if (!string.IsNullOrEmpty(serviceJson)) {
            record.ProviderData[GmailPendingMessageSender.ServiceAccountJsonProtectedKey] = protector.Protect(serviceJson!);
        }

        var serviceSubject = credential?.ServiceAccountSubject;
        if (!string.IsNullOrEmpty(serviceSubject)) {
            record.ProviderData[GmailPendingMessageSender.ServiceAccountSubjectKey] = serviceSubject!;
        }

        try {
            await PendingMessageRepository.SaveAsync(record, cancellationToken).ConfigureAwait(false);
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Failed to persist Gmail pending message: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends the specified MIME message via Gmail API.
    /// </summary>
    public async Task<GmailMessage> SendAsync(string userId, MimeMessage message, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }
        if (DryRun) {
            return new GmailMessage { Id = string.Empty, ThreadId = string.Empty };
        }
        using var ms = new MemoryStream();
        await message.WriteToAsync(ms, cancellationToken).ConfigureAwait(false);
        var raw = Convert.ToBase64String(ms.ToArray())
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", string.Empty);
        var json = JsonSerializer.Serialize(new GmailRawRequest(raw), MailozaurrJsonContext.Default.GmailRawRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync($"users/{userId}/messages/send", content, cancellationToken).ConfigureAwait(false);
        var queued = false;
        try {
            await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
#if NET5_0_OR_GREATER
                var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
                await QueuePendingMessageAsync(userId, message, cancellationToken).ConfigureAwait(false);
                queued = true;
                throw new HttpRequestException($"Gmail returned {(int)response.StatusCode} ({response.StatusCode}): {error}");
            }
        } catch (GmailAuthenticationException) {
            if (!queued) {
                await QueuePendingMessageAsync(userId, message, cancellationToken).ConfigureAwait(false);
                queued = true;
            }
            throw;
        } catch (HttpRequestException) {
            if (!queued) {
                await QueuePendingMessageAsync(userId, message, cancellationToken).ConfigureAwait(false);
                queued = true;
            }
            throw;
        } catch (TaskCanceledException) {
            if (!queued) {
                await QueuePendingMessageAsync(userId, message, cancellationToken).ConfigureAwait(false);
                queued = true;
            }
            throw;
        }
#if NET5_0_OR_GREATER
        var resultJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var resultJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailMessage? result;
        try {
            result = JsonSerializer.Deserialize(resultJson, MailozaurrJsonContext.Default.GmailMessage);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API send response.", resultJson, ex);
        }
        if (result is null) {
            throw new InvalidDataException("Gmail API returned an invalid send response.");
        }
        return result;
    }

    /// <summary>
    /// Lists messages matching the supplied query.
    /// </summary>
    public Task<IList<GmailMessage>> ListAsync(string userId, string? query = null, int? maxResults = null, CancellationToken cancellationToken = default) =>
        ListAdvancedAsync(userId, query, labelIds: null, includeSpamTrash: false, maxResultsTotal: maxResults, fields: null, cancellationToken: cancellationToken);

    private static int? ClampMaxResults(int? maxResults) {
        if (!maxResults.HasValue) {
            return null;
        }
        var v = maxResults.Value;
        if (v < 1) {
            return 1;
        }
        if (v > 500) {
            return 500;
        }
        return v;
    }

    /// <summary>
    /// Lists a single page of messages.
    /// </summary>
    public async Task<GmailListResponse> ListPageAsync(
        string userId,
        string? query = null,
        IReadOnlyList<string>? labelIds = null,
        bool includeSpamTrash = false,
        int? maxResults = null,
        string? pageToken = null,
        string? fields = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        var url = new StringBuilder($"users/{userId}/messages");
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(query)) qs.Add($"q={Uri.EscapeDataString(query)}");
        var safeMax = ClampMaxResults(maxResults);
        if (safeMax.HasValue) qs.Add($"maxResults={safeMax.Value}");
        if (!string.IsNullOrWhiteSpace(pageToken)) qs.Add($"pageToken={Uri.EscapeDataString(pageToken)}");
        if (includeSpamTrash) qs.Add("includeSpamTrash=true");
        if (labelIds != null) {
            for (var i = 0; i < labelIds.Count; i++) {
                var lid = labelIds[i];
                if (!string.IsNullOrWhiteSpace(lid)) {
                    qs.Add($"labelIds={Uri.EscapeDataString(lid.Trim())}");
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(fields)) qs.Add($"fields={Uri.EscapeDataString(fields)}");
        if (qs.Count > 0) {
            url.Append('?').Append(string.Join("&", qs));
        }

        using var response = await _client.GetAsync(url.ToString(), cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailListResponse? list;
        try {
            list = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailListResponse);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API list response.", json, ex);
        }
        if (list is null) {
            throw new InvalidDataException("Gmail API returned an invalid list response.");
        }
        return list;
    }

    /// <summary>
    /// Lists messages matching the supplied query.
    /// </summary>
    public async Task<IList<GmailMessage>> ListAdvancedAsync(
        string userId,
        string? query = null,
        IReadOnlyList<string>? labelIds = null,
        bool includeSpamTrash = false,
        int? maxResultsTotal = null,
        string? fields = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        var messages = new List<GmailMessage>();
        string? pageToken = null;
        int? remaining = maxResultsTotal;
        while (true) {
            var list = await ListPageAsync(
                userId,
                query: query,
                labelIds: labelIds,
                includeSpamTrash: includeSpamTrash,
                maxResults: remaining,
                pageToken: pageToken,
                fields: fields,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (list.Messages != null) {
                messages.AddRange(list.Messages);
            }
            if (maxResultsTotal.HasValue && messages.Count >= maxResultsTotal.Value) {
                break;
            }
            pageToken = list.NextPageToken;
            if (string.IsNullOrEmpty(pageToken)) {
                break;
            }
            if (maxResultsTotal.HasValue) {
                remaining = maxResultsTotal.Value - messages.Count;
            }
        }

        if (maxResultsTotal.HasValue && messages.Count > maxResultsTotal.Value) {
            messages = messages.GetRange(0, maxResultsTotal.Value);
        }
        return messages;
    }

    /// <summary>
    /// Retrieves a single message by id.
    /// </summary>
    public async Task<GmailMessage> GetAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        using var response = await _client.GetAsync($"users/{userId}/messages/{id}", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailMessage? message;
        try {
            message = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailMessage);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API message response.", json, ex);
        }
        if (message is null) {
            throw new InvalidDataException("Gmail API returned an invalid message response.");
        }
        return message;
    }

    /// <summary>
    /// Retrieves a single message by id, optionally requesting a specific format/fields subset and metadata headers.
    /// </summary>
    /// <remarks>
    /// This is a low-level helper for callers that need Gmail partial responses or <c>format=metadata</c>.
    /// Prefer <see cref="GetFullAsync"/> and <see cref="GetRawAsync"/> when possible.
    /// </remarks>
    public Task<GmailMessage> GetMessageWithOptionsAsync(
        string userId,
        string id,
        string? format = null,
        IReadOnlyCollection<string>? metadataHeaders = null,
        string? fields = null,
        CancellationToken cancellationToken = default)
        => GetMessageWithOptionsCoreAsync(userId, id, format, metadataHeaders, fields, cancellationToken);

    private async Task<GmailMessage> GetMessageWithOptionsCoreAsync(
        string userId,
        string id,
        string? format,
        IReadOnlyCollection<string>? metadataHeaders,
        string? fields,
        CancellationToken cancellationToken) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(id)) {
            throw new ArgumentException("id is required.", nameof(id));
        }

        var safeId = Uri.EscapeDataString(id.Trim());
        var url = new StringBuilder($"users/{userId}/messages/{safeId}");
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(format)) qs.Add($"format={Uri.EscapeDataString(format!.Trim())}");
        if (!string.IsNullOrWhiteSpace(fields)) qs.Add($"fields={Uri.EscapeDataString(fields!.Trim())}");
        if (metadataHeaders != null) {
            foreach (var h in metadataHeaders) {
                if (!string.IsNullOrWhiteSpace(h)) {
                    qs.Add($"metadataHeaders={Uri.EscapeDataString(h.Trim())}");
                }
            }
        }
        if (qs.Count > 0) {
            url.Append('?').Append(string.Join("&", qs));
        }

        using var response = await _client.GetAsync(url.ToString(), cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailMessage? message;
        try {
            message = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailMessage);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API message response.", json, ex);
        }
        if (message is null) {
            throw new InvalidDataException("Gmail API returned an invalid message response.");
        }
        return message;
    }

    private Task<GmailMessage> GetMessageWithFormatAsync(
        string userId,
        string id,
        string format,
        string? fields,
        CancellationToken cancellationToken)
        => GetMessageWithOptionsCoreAsync(userId, id, format, metadataHeaders: null, fields: fields, cancellationToken);

    /// <summary>
    /// Retrieves a MIME message by id.
    /// </summary>
    public async Task<MimeMessage> GetMimeMessageAsync(string userId, string id, CancellationToken cancellationToken = default) {
        var msg = await GetMessageWithFormatAsync(userId, id, format: "raw", fields: null, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(msg.Raw)) {
            throw new InvalidDataException("Gmail API returned an invalid message response.");
        }
        var raw = msg.Raw!;
        var data = raw.Replace('-', '+').Replace('_', '/');
        int padding = (4 - data.Length % 4) % 4;
        if (padding > 0) data = data.PadRight(data.Length + padding, '=');
        var bytes = Convert.FromBase64String(data);
        using var ms = new MemoryStream(bytes);
        return MimeMessage.Load(ms);
    }

    /// <summary>
    /// Retrieves a raw message (base64url MIME) by id.
    /// </summary>
    public Task<GmailMessage> GetRawAsync(string userId, string id, string? fields = null, CancellationToken cancellationToken = default) =>
        GetMessageWithFormatAsync(userId, id, format: "raw", fields: fields, cancellationToken: cancellationToken);

    /// <summary>
    /// Retrieves a full message by id.
    /// </summary>
    public Task<GmailMessage> GetFullAsync(string userId, string id, string? fields = null, CancellationToken cancellationToken = default) =>
        GetMessageWithFormatAsync(userId, id, format: "full", fields: fields, cancellationToken: cancellationToken);

    /// <summary>
    /// Imports a message (MIME base64url) into a mailbox.
    /// </summary>
    /// <remarks>
    /// This calls <c>users.messages.import</c>. It is typically used to append a sent copy into Gmail.
    /// </remarks>
    public async Task<GmailMessage> ImportAsync(
        string userId,
        string raw,
        IReadOnlyCollection<string>? labelIds = null,
        string internalDateSource = "dateHeader",
        bool neverMarkSpam = true,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(raw)) {
            throw new ArgumentException("raw is required.", nameof(raw));
        }
        if (DryRun) {
            return new GmailMessage { Id = string.Empty, ThreadId = string.Empty };
        }

        var url = new StringBuilder($"users/{userId}/messages/import");
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(internalDateSource)) qs.Add($"internalDateSource={Uri.EscapeDataString(internalDateSource.Trim())}");
        qs.Add($"neverMarkSpam={(neverMarkSpam ? "true" : "false")}");
        if (qs.Count > 0) {
            url.Append('?').Append(string.Join("&", qs));
        }

        var request = new GmailImportMessageRequest {
            Raw = raw.Trim(),
            LabelIds = labelIds is null || labelIds.Count == 0 ? null : new List<string>(labelIds)
        };
        var jsonRequest = JsonSerializer.Serialize(request, MailozaurrJsonContext.Default.GmailImportMessageRequest);
        using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync(url.ToString(), content, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailMessage? message;
        try {
            message = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailMessage);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API import response.", json, ex);
        }
        if (message is null) {
            throw new InvalidDataException("Gmail API returned an invalid import response.");
        }
        return message;
    }

    /// <summary>
    /// Deletes a message by id.
    /// </summary>
    public async Task DeleteAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return;
        }
        using var response = await _client.DeleteAsync($"users/{userId}/messages/{id}", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Moves a message to trash.
    /// </summary>
    public async Task<GmailMessage> TrashMessageAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return new GmailMessage { Id = id, ThreadId = string.Empty };
        }
        using var response = await _client.PostAsync($"users/{userId}/messages/{id}/trash", content: null, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailMessage? message;
        try {
            message = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailMessage);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API trash response.", json, ex);
        }
        if (message is null) {
            throw new InvalidDataException("Gmail API returned an invalid trash response.");
        }
        return message;
    }

    /// <summary>
    /// Modifies labels on a single message.
    /// </summary>
    public async Task<GmailMessage> ModifyMessageLabelsAsync(
        string userId,
        string id,
        IReadOnlyCollection<string>? addLabelIds = null,
        IReadOnlyCollection<string>? removeLabelIds = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return new GmailMessage { Id = id, ThreadId = string.Empty };
        }
        var request = new GmailModifyLabelsRequest {
            AddLabelIds = addLabelIds ?? Array.Empty<string>(),
            RemoveLabelIds = removeLabelIds ?? Array.Empty<string>()
        };
        var jsonRequest = JsonSerializer.Serialize(request, MailozaurrJsonContext.Default.GmailModifyLabelsRequest);
        using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync($"users/{userId}/messages/{id}/modify", content, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailMessage? message;
        try {
            message = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailMessage);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API message modify response.", json, ex);
        }
        if (message is null) {
            throw new InvalidDataException("Gmail API returned an invalid message modify response.");
        }
        return message;
    }

    /// <summary>
    /// Modifies labels on multiple messages.
    /// </summary>
    public async Task BatchModifyMessagesAsync(
        string userId,
        IReadOnlyCollection<string> ids,
        IReadOnlyCollection<string>? addLabelIds = null,
        IReadOnlyCollection<string>? removeLabelIds = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return;
        }
        if (ids == null) {
            throw new ArgumentNullException(nameof(ids));
        }
        if (ids.Count == 0) {
            return;
        }
        var request = new GmailBatchModifyRequest {
            Ids = ids,
            AddLabelIds = addLabelIds ?? Array.Empty<string>(),
            RemoveLabelIds = removeLabelIds ?? Array.Empty<string>()
        };
        var jsonRequest = JsonSerializer.Serialize(request, MailozaurrJsonContext.Default.GmailBatchModifyRequest);
        using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync($"users/{userId}/messages/batchModify", content, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Deletes multiple messages.
    /// </summary>
    public async Task BatchDeleteMessagesAsync(string userId, IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return;
        }
        if (ids == null) {
            throw new ArgumentNullException(nameof(ids));
        }
        if (ids.Count == 0) {
            return;
        }
        var request = new GmailBatchDeleteRequest { Ids = ids };
        var jsonRequest = JsonSerializer.Serialize(request, MailozaurrJsonContext.Default.GmailBatchDeleteRequest);
        using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync($"users/{userId}/messages/batchDelete", content, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Lists labels for the specified user.
    /// </summary>
    public async Task<IList<GmailLabel>> ListLabelsAsync(string userId, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        using var response = await _client.GetAsync($"users/{userId}/labels?fields=labels(id,name,type)", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailLabelListResponse? list;
        try {
            list = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailLabelListResponse);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API labels response.", json, ex);
        }
        return (IList<GmailLabel>)(list?.Labels ?? new List<GmailLabel>());
    }

    /// <summary>
    /// Modifies labels on a thread.
    /// </summary>
    public async Task<GmailThread> ModifyThreadLabelsAsync(
        string userId,
        string id,
        IReadOnlyCollection<string>? addLabelIds = null,
        IReadOnlyCollection<string>? removeLabelIds = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return new GmailThread { Id = id, Messages = new List<GmailMessage>() };
        }
        var request = new GmailModifyLabelsRequest {
            AddLabelIds = addLabelIds ?? Array.Empty<string>(),
            RemoveLabelIds = removeLabelIds ?? Array.Empty<string>()
        };
        var jsonRequest = JsonSerializer.Serialize(request, MailozaurrJsonContext.Default.GmailModifyLabelsRequest);
        using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync($"users/{userId}/threads/{id}/modify", content, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailThread? thread;
        try {
            thread = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailThread);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API thread modify response.", json, ex);
        }
        if (thread is null) {
            throw new InvalidDataException("Gmail API returned an invalid thread modify response.");
        }
        return thread;
    }

    /// <summary>
    /// Moves a thread to trash.
    /// </summary>
    public async Task<GmailThread> TrashThreadAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return new GmailThread { Id = id, Messages = new List<GmailMessage>() };
        }
        using var response = await _client.PostAsync($"users/{userId}/threads/{id}/trash", content: null, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailThread? thread;
        try {
            thread = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailThread);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API thread trash response.", json, ex);
        }
        if (thread is null) {
            throw new InvalidDataException("Gmail API returned an invalid thread trash response.");
        }
        return thread;
    }

    /// <summary>
    /// Deletes a thread by id.
    /// </summary>
    public async Task DeleteThreadAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return;
        }
        using var response = await _client.DeleteAsync($"users/{userId}/threads/{id}", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Gets the Gmail profile for the specified user.
    /// </summary>
    public async Task<GmailProfile> GetProfileAsync(string userId, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        using var response = await _client.GetAsync($"users/{userId}/profile", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailProfile? profile;
        try {
            profile = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailProfile);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API profile response.", json, ex);
        }
        if (profile is null) {
            throw new InvalidDataException("Gmail API returned an invalid profile response.");
        }
        return profile;
    }

    /// <summary>
    /// Starts a Gmail push notification watch for the specified topic.
    /// </summary>
    /// <remarks>
    /// This calls <c>users.watch</c> and returns the watch response (historyId + expiration).
    /// </remarks>
    public async Task<GmailWatchResponse> WatchAsync(
        string userId,
        string topicName,
        IReadOnlyList<string>? labelIds = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(topicName)) {
            throw new ArgumentException("topicName is required.", nameof(topicName));
        }
        if (DryRun) {
            return new GmailWatchResponse { HistoryId = string.Empty, Expiration = 0 };
        }

        var request = new GmailWatchRequest { TopicName = topicName };
        if (labelIds != null && labelIds.Count > 0) {
            request.LabelIds = new List<string>(labelIds);
            request.LabelFilterAction = "include";
        }

        var body = JsonSerializer.Serialize(request, MailozaurrJsonContext.Default.GmailWatchRequest);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync($"users/{userId}/watch", content, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailWatchResponse? watch;
        try {
            watch = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailWatchResponse);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API watch response.", json, ex);
        }
        if (watch is null) {
            throw new InvalidDataException("Gmail API returned an invalid watch response.");
        }
        return watch;
    }

    /// <summary>
    /// Stops Gmail push notification watches for the specified user.
    /// </summary>
    /// <remarks>
    /// This calls <c>users.stop</c>.
    /// </remarks>
    public async Task StopWatchAsync(string userId, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (DryRun) {
            return;
        }
        using var response = await _client.PostAsync($"users/{userId}/stop", new StringContent("{}", Encoding.UTF8, "application/json"), cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
#if NET5_0_OR_GREATER
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            throw new GmailApiException(
                response.StatusCode,
                $"Gmail users.stop failed ({(int)response.StatusCode}).",
                content);
        }
    }

    /// <summary>
    /// Lists Gmail history changes for the specified user.
    /// </summary>
    public async Task<GmailHistoryListResponse> ListHistoryAsync(
        string userId,
        string startHistoryId,
        string? labelId = null,
        IReadOnlyList<string>? historyTypes = null,
        int? maxResults = null,
        string? pageToken = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(startHistoryId)) {
            throw new ArgumentException("startHistoryId is required.", nameof(startHistoryId));
        }

        var url = new StringBuilder($"users/{userId}/history");
        var qs = new List<string> {
            $"startHistoryId={Uri.EscapeDataString(startHistoryId)}"
        };
        if (!string.IsNullOrWhiteSpace(labelId)) qs.Add($"labelId={Uri.EscapeDataString(labelId)}");
        if (maxResults.HasValue) qs.Add($"maxResults={maxResults.Value}");
        if (!string.IsNullOrWhiteSpace(pageToken)) qs.Add($"pageToken={Uri.EscapeDataString(pageToken)}");
        if (historyTypes != null) {
            for (var i = 0; i < historyTypes.Count; i++) {
                var t = historyTypes[i];
                if (!string.IsNullOrWhiteSpace(t)) {
                    qs.Add($"historyTypes={Uri.EscapeDataString(t)}");
                }
            }
        }
        if (qs.Count > 0) {
            url.Append('?').Append(string.Join("&", qs));
        }

        using var response = await _client.GetAsync(url.ToString(), cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailHistoryListResponse? history;
        try {
            history = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailHistoryListResponse);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API history response.", json, ex);
        }
        if (history is null) {
            throw new InvalidDataException("Gmail API returned an invalid history response.");
        }
        return history;
    }

    /// <summary>
    /// Lists threads matching the supplied query.
    /// </summary>
    public async Task<IList<GmailThreadInfo>> ListThreadsAsync(string userId, string? query = null, int? maxResults = null, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        var threads = new List<GmailThreadInfo>();
        string? pageToken = null;
        do {
            var url = new StringBuilder($"users/{userId}/threads");
            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(query)) qs.Add($"q={Uri.EscapeDataString(query)}");
            if (maxResults.HasValue) qs.Add($"maxResults={maxResults.Value}");
            if (!string.IsNullOrEmpty(pageToken)) qs.Add($"pageToken={pageToken}");
            if (qs.Count > 0) {
                url.Append('?').Append(string.Join("&", qs));
            }
            using var response = await _client.GetAsync(url.ToString(), cancellationToken).ConfigureAwait(false);
            await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            GmailThreadListResponse? list;
            try {
                list = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailThreadListResponse);
            } catch (JsonException ex) {
                throw new GmailApiException("Failed to parse Gmail API thread list response.", json, ex);
            }
            if (list?.Threads != null) {
                threads.AddRange(list.Threads);
            }
            pageToken = list?.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return threads;
    }

    /// <summary>
    /// Retrieves a single thread by id.
    /// </summary>
    public async Task<GmailThread> GetThreadAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        using var response = await _client.GetAsync($"users/{userId}/threads/{id}", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailThread? thread;
        try {
            thread = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailThread);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API thread response.", json, ex);
        }
        if (thread is null) {
            throw new InvalidDataException("Gmail API returned an invalid thread response.");
        }
        return thread;
    }

    /// <summary>
    /// Retrieves a single thread by id, optionally requesting a partial response.
    /// </summary>
    public async Task<GmailThread> GetThreadWithOptionsAsync(
        string userId,
        string id,
        string? format = null,
        string? fields = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(id)) {
            throw new ArgumentException("id is required.", nameof(id));
        }

        var safeId = Uri.EscapeDataString(id.Trim());
        var url = new StringBuilder($"users/{userId}/threads/{safeId}");
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(format)) qs.Add($"format={Uri.EscapeDataString(format!.Trim())}");
        if (!string.IsNullOrWhiteSpace(fields)) qs.Add($"fields={Uri.EscapeDataString(fields!.Trim())}");
        if (qs.Count > 0) {
            url.Append('?').Append(string.Join("&", qs));
        }

        using var response = await _client.GetAsync(url.ToString(), cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailThread? thread;
        try {
            thread = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailThread);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API thread response.", json, ex);
        }
        if (thread is null) {
            throw new InvalidDataException("Gmail API returned an invalid thread response.");
        }
        return thread;
    }

    /// <summary>
    /// Lists attachment metadata for a message.
    /// </summary>
    public async Task<IList<GmailAttachmentInfo>> ListAttachmentsAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        using var response = await _client.GetAsync($"users/{userId}/messages/{id}?format=full", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var list = new List<GmailAttachmentInfo>();
        if (doc.RootElement.TryGetProperty("payload", out var payload)) {
            ExtractAttachments(payload, list);
        }
        return list;
    }

    /// <summary>
    /// Downloads a single attachment by id.
    /// </summary>
    public async Task<byte[]> DownloadAttachmentAsync(string userId, string messageId, string attachmentId, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        using var response = await _client.GetAsync($"users/{userId}/messages/{messageId}/attachments/{attachmentId}", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        AttachmentResponse? result;
        try {
            result = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.AttachmentResponse);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API attachment response.", json, ex);
        }
        if (string.IsNullOrEmpty(result?.Data)) {
            return Array.Empty<byte>();
        }

        var dataProp = result!.Data!;
        var data = dataProp.Replace('-', '+').Replace('_', '/');

        if (data.Length % 4 == 1) {
            throw new InvalidDataException("Attachment data is not a valid Base64 string.");
        }

        int padding = (4 - data.Length % 4) % 4;
        if (padding > 0) {
            data = data.PadRight(data.Length + padding, '=');
        }

        try {
            return Convert.FromBase64String(data);
        } catch (FormatException ex) {
            throw new InvalidDataException("Attachment data is not a valid Base64 string.", ex);
        }
    }

    private static void ExtractAttachments(JsonElement part, List<GmailAttachmentInfo> list) {
        string? fileName = part.GetProperty("filename").GetString();
        string? mime = part.GetProperty("mimeType").GetString();
        if (!string.IsNullOrEmpty(fileName) &&
            part.TryGetProperty("body", out var body) &&
            body.TryGetProperty("attachmentId", out var idProp)) {
            list.Add(new GmailAttachmentInfo { Id = idProp.GetString(), FileName = fileName, MimeType = mime });
        }
        if (part.TryGetProperty("parts", out var parts)) {
            foreach (var p in parts.EnumerateArray()) {
                ExtractAttachments(p, list);
            }
        }
    }

    /// <summary>Attachment metadata returned by Gmail API.</summary>
    public sealed class AttachmentResponse {
        /// <summary>Base64 encoded attachment data.</summary>
        public string? Data { get; set; }
    }

    /// <summary>Response envelope for Gmail list messages API.</summary>
    public sealed class GmailListResponse {
        /// <summary>Messages returned by the API.</summary>
        public List<GmailMessage>? Messages { get; set; }
        /// <summary>Token for the next page of results.</summary>
        public string? NextPageToken { get; set; }
        /// <summary>Estimated total number of results.</summary>
        [JsonPropertyName("resultSizeEstimate")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long? ResultSizeEstimate { get; set; }
    }

    /// <summary>Response envelope for Gmail thread listing.</summary>
    public sealed class GmailThreadListResponse {
        /// <summary>Threads returned by the API.</summary>
        public List<GmailThreadInfo>? Threads { get; set; }
        /// <summary>Token for the next page of results.</summary>
        public string? NextPageToken { get; set; }
    }

    /// <summary>Request payload for Gmail watch API.</summary>
    public sealed class GmailWatchRequest {
        /// <summary>Pub/Sub topic name to deliver notifications to.</summary>
        [JsonPropertyName("topicName")]
        public string TopicName { get; set; } = string.Empty;
        /// <summary>Optional label filters.</summary>
        [JsonPropertyName("labelIds")]
        public List<string>? LabelIds { get; set; }
        /// <summary>Action to apply to the label filter (usually <c>include</c>).</summary>
        [JsonPropertyName("labelFilterAction")]
        public string? LabelFilterAction { get; set; }
    }

    /// <summary>Response payload for Gmail watch API.</summary>
    public sealed class GmailWatchResponse {
        /// <summary>History id at the start of the watch.</summary>
        [JsonPropertyName("historyId")]
        public string? HistoryId { get; set; }
        /// <summary>Watch expiration as milliseconds since epoch.</summary>
        [JsonPropertyName("expiration")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long Expiration { get; set; }
    }

    /// <summary>Gmail profile response.</summary>
    public sealed class GmailProfile {
        /// <summary>Email address associated with the mailbox.</summary>
        [JsonPropertyName("emailAddress")]
        public string? EmailAddress { get; set; }
        /// <summary>Total number of messages.</summary>
        [JsonPropertyName("messagesTotal")]
        public long MessagesTotal { get; set; }
        /// <summary>Total number of threads.</summary>
        [JsonPropertyName("threadsTotal")]
        public long ThreadsTotal { get; set; }
        /// <summary>Current history id.</summary>
        [JsonPropertyName("historyId")]
        public string? HistoryId { get; set; }
    }

    /// <summary>Gmail history list response.</summary>
    public sealed class GmailHistoryListResponse {
        /// <summary>History records.</summary>
        [JsonPropertyName("history")]
        public List<GmailHistoryRecord>? History { get; set; }
        /// <summary>Token for the next page of results.</summary>
        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; set; }
        /// <summary>Latest history id.</summary>
        [JsonPropertyName("historyId")]
        public string? HistoryId { get; set; }
    }

    /// <summary>History record returned by Gmail history API.</summary>
    public sealed class GmailHistoryRecord {
        /// <summary>History record id.</summary>
        public string? Id { get; set; }
        /// <summary>Messages added in this history record.</summary>
        public List<GmailHistoryMessageAdded>? MessagesAdded { get; set; }
        /// <summary>Messages deleted in this history record.</summary>
        public List<GmailHistoryMessageDeleted>? MessagesDeleted { get; set; }
        /// <summary>Labels added in this history record.</summary>
        public List<GmailHistoryLabelAdded>? LabelsAdded { get; set; }
        /// <summary>Labels removed in this history record.</summary>
        public List<GmailHistoryLabelRemoved>? LabelsRemoved { get; set; }
    }

    /// <summary>History wrapper for a message added event.</summary>
    public sealed class GmailHistoryMessageAdded {
        /// <summary>Message reference associated with the event.</summary>
        public GmailHistoryMessageRef? Message { get; set; }
    }

    /// <summary>History wrapper for a message deleted event.</summary>
    public sealed class GmailHistoryMessageDeleted {
        /// <summary>Message reference associated with the event.</summary>
        public GmailHistoryMessageRef? Message { get; set; }
    }

    /// <summary>History wrapper for a label added event.</summary>
    public sealed class GmailHistoryLabelAdded {
        /// <summary>Message reference associated with the event.</summary>
        public GmailHistoryMessageRef? Message { get; set; }
        /// <summary>Label ids associated with the event.</summary>
        public List<string>? LabelIds { get; set; }
    }

    /// <summary>History wrapper for a label removed event.</summary>
    public sealed class GmailHistoryLabelRemoved {
        /// <summary>Message reference associated with the event.</summary>
        public GmailHistoryMessageRef? Message { get; set; }
        /// <summary>Label ids associated with the event.</summary>
        public List<string>? LabelIds { get; set; }
    }

    /// <summary>Reference to a Gmail message returned by the history API.</summary>
    public sealed class GmailHistoryMessageRef {
        /// <summary>Message id.</summary>
        public string? Id { get; set; }
        /// <summary>Thread id.</summary>
        public string? ThreadId { get; set; }
    }

    /// <summary>Response envelope for Gmail list labels API.</summary>
    public sealed class GmailLabelListResponse {
        /// <summary>Labels returned by the API.</summary>
        public List<GmailLabel>? Labels { get; set; }
    }

    /// <summary>Request payload for Gmail modify label endpoints.</summary>
    public sealed class GmailModifyLabelsRequest {
        /// <summary>Label ids to add.</summary>
        [JsonPropertyName("addLabelIds")]
        public IReadOnlyCollection<string> AddLabelIds { get; set; } = Array.Empty<string>();

        /// <summary>Label ids to remove.</summary>
        [JsonPropertyName("removeLabelIds")]
        public IReadOnlyCollection<string> RemoveLabelIds { get; set; } = Array.Empty<string>();
    }

    /// <summary>Request payload for Gmail batch modify messages endpoint.</summary>
    public sealed class GmailBatchModifyRequest {
        /// <summary>Message ids.</summary>
        [JsonPropertyName("ids")]
        public IReadOnlyCollection<string> Ids { get; set; } = Array.Empty<string>();

        /// <summary>Label ids to add.</summary>
        [JsonPropertyName("addLabelIds")]
        public IReadOnlyCollection<string> AddLabelIds { get; set; } = Array.Empty<string>();

        /// <summary>Label ids to remove.</summary>
        [JsonPropertyName("removeLabelIds")]
        public IReadOnlyCollection<string> RemoveLabelIds { get; set; } = Array.Empty<string>();
    }

    /// <summary>Request payload for Gmail batch delete messages endpoint.</summary>
    public sealed class GmailBatchDeleteRequest {
        /// <summary>Message ids.</summary>
        [JsonPropertyName("ids")]
        public IReadOnlyCollection<string> Ids { get; set; } = Array.Empty<string>();
    }

    /// <summary>Request payload for Gmail import message endpoint.</summary>
    public sealed class GmailImportMessageRequest {
        /// <summary>Raw message content as base64url.</summary>
        [JsonPropertyName("raw")]
        public string Raw { get; set; } = string.Empty;

        /// <summary>Optional label ids to apply to the imported message.</summary>
        [JsonPropertyName("labelIds")]
        public List<string>? LabelIds { get; set; }
    }
}
