using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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

    internal GmailApiClient(HttpClient client, Func<CancellationToken, Task<string>>? refreshToken = null, OAuthCredential? credential = null) {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _refreshToken = refreshToken;
        _credential = credential;
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
            MessageId = message.MessageId,
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
    public async Task<IList<GmailMessage>> ListAsync(string userId, string? query = null, int? maxResults = null, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        var messages = new List<GmailMessage>();
        string? pageToken = null;
        int? remaining = maxResults;
        while (true) {
            var url = new StringBuilder($"users/{userId}/messages");
            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(query)) qs.Add($"q={Uri.EscapeDataString(query)}");
            if (remaining.HasValue) qs.Add($"maxResults={remaining.Value}");
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
            GmailListResponse? list;
            try {
                list = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailListResponse);
            } catch (JsonException ex) {
                throw new GmailApiException("Failed to parse Gmail API list response.", json, ex);
            }
            if (list?.Messages != null) {
                messages.AddRange(list.Messages);
            }
            if (maxResults.HasValue && messages.Count >= maxResults.Value) {
                break;
            }
            pageToken = list?.NextPageToken;
            if (string.IsNullOrEmpty(pageToken)) {
                break;
            }
            if (maxResults.HasValue) {
                remaining = maxResults.Value - messages.Count;
            }
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
    /// Retrieves a MIME message by id.
    /// </summary>
    public async Task<MimeMessage> GetMimeMessageAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        using var response = await _client.GetAsync($"users/{userId}/messages/{id}?format=raw", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        GmailMessage? msg;
        try {
            msg = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailMessage);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API message response.", json, ex);
        }
        if (string.IsNullOrEmpty(msg?.Raw)) {
            throw new InvalidDataException("Gmail API returned an invalid message response.");
        }
        var raw = msg!.Raw!;
        var data = raw.Replace('-', '+').Replace('_', '/');
        int padding = (4 - data.Length % 4) % 4;
        if (padding > 0) data = data.PadRight(data.Length + padding, '=');
        var bytes = Convert.FromBase64String(data);
        using var ms = new MemoryStream(bytes);
        return MimeMessage.Load(ms);
    }

    /// <summary>
    /// Deletes a message by id.
    /// </summary>
    public async Task DeleteAsync(string userId, string id, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        using var response = await _client.DeleteAsync($"users/{userId}/messages/{id}", cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
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
    }

    /// <summary>Response envelope for Gmail thread listing.</summary>
    public sealed class GmailThreadListResponse {
        /// <summary>Threads returned by the API.</summary>
        public List<GmailThreadInfo>? Threads { get; set; }
        /// <summary>Token for the next page of results.</summary>
        public string? NextPageToken { get; set; }
    }
}
