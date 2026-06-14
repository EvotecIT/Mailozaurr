using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Mailozaurr.Application;

/// <summary>
/// Builds Gmail API sessions from reusable profile and secret stores.
/// </summary>
public sealed class GmailSessionFactory : IGmailSessionFactory {
    private static readonly Uri GoogleTokenEndpoint = new("https://oauth2.googleapis.com/token");
    private readonly IMailSecretStore _secretStore;
    private readonly Func<GmailRefreshRequest, CancellationToken, Task<OAuthCredential>> _refreshCredentialAsync;
    private readonly Func<GmailSessionRequest, CancellationToken, Task<GmailSession>> _connectAsync;

    /// <summary>
    /// Creates a new Gmail session factory.
    /// </summary>
    public GmailSessionFactory(
        IMailSecretStore secretStore,
        Func<GmailRefreshRequest, CancellationToken, Task<OAuthCredential>>? refreshCredentialAsync = null,
        Func<GmailSessionRequest, CancellationToken, Task<GmailSession>>? connectAsync = null) {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _refreshCredentialAsync = refreshCredentialAsync ?? DefaultRefreshCredentialAsync;
        _connectAsync = connectAsync ?? DefaultConnectAsync;
    }

    /// <inheritdoc />
    public async Task<GmailSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) {
        if (profile == null) {
            throw new ArgumentNullException(nameof(profile));
        }

        var userId = ResolveUserId(profile);
        var (credential, refreshRequest) = await ResolveCredentialAsync(profile, userId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(credential.UserName)) {
            credential.UserName = userId;
        }
        if (credential.ExpiresOn == default) {
            credential.ExpiresOn = DateTimeOffset.MaxValue;
        }

        Func<CancellationToken, Task<string>>? refreshDelegate = null;
        if (refreshRequest != null) {
            refreshDelegate = async token => {
                var refreshed = await _refreshCredentialAsync(refreshRequest, token).ConfigureAwait(false);
                credential.AccessToken = refreshed.AccessToken;
                credential.ExpiresOn = refreshed.ExpiresOn;
                credential.RefreshToken = refreshed.RefreshToken ?? credential.RefreshToken;
                return refreshed.AccessToken;
            };
        }

        return await _connectAsync(new GmailSessionRequest {
            UserId = userId,
            Credential = credential,
            RefreshAccessTokenAsync = refreshDelegate
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(OAuthCredential Credential, GmailRefreshRequest? RefreshRequest)> ResolveCredentialAsync(
        MailProfile profile,
        string userId,
        CancellationToken cancellationToken) {
        var accessToken = await _secretStore.GetSecretAsync(profile.Id, MailSecretNames.AccessToken, cancellationToken).ConfigureAwait(false);
        var refreshToken = await _secretStore.GetSecretAsync(profile.Id, MailSecretNames.RefreshToken, cancellationToken).ConfigureAwait(false);
        var tokenExpiresOn = TryResolveTokenExpiration(profile);
        profile.Settings.TryGetValue(MailProfileSettingsKeys.ClientId, out var clientId);
        var clientSecret = await _secretStore.GetSecretAsync(profile.Id, MailSecretNames.ClientSecret, cancellationToken).ConfigureAwait(false);

        GmailRefreshRequest? refreshRequest = null;
        if (!string.IsNullOrWhiteSpace(refreshToken) &&
            !string.IsNullOrWhiteSpace(clientId) &&
            !string.IsNullOrWhiteSpace(clientSecret)) {
            var normalizedClientId = clientId!.Trim();
            var normalizedClientSecret = clientSecret!.Trim();
            var normalizedRefreshToken = refreshToken!.Trim();
            refreshRequest = new GmailRefreshRequest {
                UserId = userId,
                ClientId = normalizedClientId,
                ClientSecret = normalizedClientSecret,
                RefreshToken = normalizedRefreshToken
            };
        }

        if (!string.IsNullOrWhiteSpace(accessToken) && !ShouldRefreshToken(tokenExpiresOn)) {
            var normalizedAccessToken = accessToken!.Trim();
            var normalizedRefreshToken = string.IsNullOrWhiteSpace(refreshToken) ? null : refreshToken!.Trim();
            var normalizedClientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId!.Trim();
            var normalizedClientSecret = string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret!.Trim();
            return (new OAuthCredential {
                UserName = userId,
                AccessToken = normalizedAccessToken,
                RefreshToken = normalizedRefreshToken,
                ClientId = normalizedClientId,
                ClientSecret = normalizedClientSecret,
                ExpiresOn = tokenExpiresOn ?? DateTimeOffset.MaxValue
            }, refreshRequest);
        }

        if (refreshRequest == null) {
            throw new InvalidOperationException(
                $"Gmail profile '{profile.Id}' requires secret '{MailSecretNames.AccessToken}' or the combination of secret '{MailSecretNames.RefreshToken}', setting '{MailProfileSettingsKeys.ClientId}', and secret '{MailSecretNames.ClientSecret}'.");
        }

        var refreshed = await _refreshCredentialAsync(refreshRequest, cancellationToken).ConfigureAwait(false);
        refreshed.UserName = userId;
        refreshed.ClientId ??= refreshRequest.ClientId;
        refreshed.ClientSecret ??= refreshRequest.ClientSecret;
        refreshed.RefreshToken ??= refreshRequest.RefreshToken;
        return (refreshed, refreshRequest);
    }

    private static async Task<OAuthCredential> DefaultRefreshCredentialAsync(
        GmailRefreshRequest request,
        CancellationToken cancellationToken) {
        using var client = new HttpClient();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> {
            ["client_id"] = request.ClientId,
            ["client_secret"] = request.ClientSecret,
            ["refresh_token"] = request.RefreshToken,
            ["grant_type"] = "refresh_token"
        });
        using var response = await client.PostAsync(GoogleTokenEndpoint, content, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!response.IsSuccessStatusCode) {
            throw new InvalidOperationException($"Gmail token refresh failed with status {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (!root.TryGetProperty("access_token", out var accessTokenElement) ||
            accessTokenElement.ValueKind != JsonValueKind.String) {
            throw new InvalidDataException("Gmail token refresh response did not contain an access_token.");
        }

        var accessToken = accessTokenElement.GetString();
        if (string.IsNullOrWhiteSpace(accessToken)) {
            throw new InvalidDataException("Gmail token refresh response contained an empty access_token.");
        }

        var expiresOn = DateTimeOffset.MaxValue;
        if (root.TryGetProperty("expires_in", out var expiresInElement)) {
            if (expiresInElement.ValueKind == JsonValueKind.Number &&
                expiresInElement.TryGetInt64(out var expiresInSeconds)) {
                expiresOn = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
            } else if (expiresInElement.ValueKind == JsonValueKind.String &&
                       long.TryParse(expiresInElement.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedExpiresInSeconds)) {
                expiresOn = DateTimeOffset.UtcNow.AddSeconds(parsedExpiresInSeconds);
            }
        }

        string? refreshToken = null;
        if (root.TryGetProperty("refresh_token", out var refreshTokenElement) &&
            refreshTokenElement.ValueKind == JsonValueKind.String) {
            refreshToken = refreshTokenElement.GetString();
        }

        return new OAuthCredential {
            UserName = request.UserId,
            AccessToken = accessToken!.Trim(),
            RefreshToken = string.IsNullOrWhiteSpace(refreshToken) ? request.RefreshToken : refreshToken!.Trim(),
            ClientId = request.ClientId,
            ClientSecret = request.ClientSecret,
            ExpiresOn = expiresOn
        };
    }

    private static Task<GmailSession> DefaultConnectAsync(GmailSessionRequest request, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new GmailSession(new GmailApiClient(request.Credential, request.RefreshAccessTokenAsync), request.UserId));
    }

    private static string ResolveUserId(MailProfile profile) {
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var mailbox) &&
            !string.IsNullOrWhiteSpace(mailbox)) {
            return mailbox.Trim();
        }
        if (!string.IsNullOrWhiteSpace(profile.DefaultMailbox)) {
            return profile.DefaultMailbox!.Trim();
        }

        return "me";
    }

    private static DateTimeOffset? TryResolveTokenExpiration(MailProfile profile) {
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.TokenExpiresOn, out var expiresOnValue) &&
            !string.IsNullOrWhiteSpace(expiresOnValue) &&
            DateTimeOffset.TryParse(expiresOnValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresOn)) {
            return expiresOn;
        }

        return null;
    }

    private static bool ShouldRefreshToken(DateTimeOffset? expiresOn) =>
        expiresOn.HasValue && expiresOn.Value <= DateTimeOffset.UtcNow.Add(MailProfileAuthDefaults.TokenRefreshWindow);

    /// <summary>
    /// Represents the refresh-token input required to mint a Gmail access token.
    /// </summary>
    public sealed class GmailRefreshRequest {
        /// <summary>Resolved Gmail user id.</summary>
        public string UserId { get; set; } = "me";

        /// <summary>OAuth client id.</summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>OAuth client secret.</summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>OAuth refresh token.</summary>
        public string RefreshToken { get; set; } = string.Empty;
    }
}