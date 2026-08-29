using MimeKit;
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

namespace Mailozaurr;

/// <summary>
/// Lightweight client for sending and retrieving messages using Gmail REST API.
/// </summary>
public sealed partial class GmailApiClient : IDisposable {
    private readonly HttpClient _client;
    private readonly Func<CancellationToken, Task<string>>? _refreshToken;
    private readonly OAuthCredential? _credential;
    private readonly bool _disposeClient;
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
        _disposeClient = true;
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
    /// <param name="ownsHttpClient">When <c>true</c>, disposing this API client also disposes <paramref name="client"/>.</param>
    public GmailApiClient(
        HttpClient client,
        Func<CancellationToken, Task<string>>? refreshToken = null,
        OAuthCredential? credential = null,
        Uri? baseAddress = null,
        bool ownsHttpClient = false) {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _refreshToken = refreshToken;
        _credential = credential;
        _disposeClient = ownsHttpClient;
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

        if (_disposeClient) {
            _client.Dispose();
        }

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
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Failed to persist Gmail pending message: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends the specified MIME message via Gmail API.
    /// </summary>
    public Task<GmailMessage> SendAsync(string userId, MimeMessage message, CancellationToken cancellationToken = default) =>
        SendCoreAsync(userId, message, onTransportAttempt: null, cancellationToken);

    internal Task<GmailMessage> SendAsync(
        string userId,
        MimeMessage message,
        Action onTransportAttempt,
        CancellationToken cancellationToken = default) =>
        SendCoreAsync(userId, message, onTransportAttempt ?? throw new ArgumentNullException(nameof(onTransportAttempt)), cancellationToken);

    private async Task<GmailMessage> SendCoreAsync(
        string userId,
        MimeMessage message,
        Action? onTransportAttempt,
        CancellationToken cancellationToken) {
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
        var json = JsonSerializer.Serialize(new GmailRawRequest(raw), GmailJsonContext.Default.GmailRawRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        onTransportAttempt?.Invoke();
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
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
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
            result = JsonSerializer.Deserialize(resultJson, GmailJsonContext.Default.GmailMessage);
        } catch (JsonException ex) {
            throw new GmailApiException("Failed to parse Gmail API send response.", resultJson, ex);
        }
        if (result is null) {
            throw new InvalidDataException("Gmail API returned an invalid send response.");
        }
        return result;
    }



}
