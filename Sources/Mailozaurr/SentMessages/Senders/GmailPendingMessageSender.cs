using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MimeKit;

namespace Mailozaurr;

/// <summary>
/// Sends queued Gmail messages using <see cref="GmailApiClient"/>.
/// </summary>
public sealed class GmailPendingMessageSender : IPendingMessageSender {
    internal const string UserIdKey = "UserId";
    internal const string AccessTokenKey = "AccessToken";
    internal const string AccessTokenBase64Key = "AccessTokenBase64";
    internal const string AccessTokenProtectedKey = "AccessTokenProtected";
    internal const string UserNameKey = "UserName";
    internal const string ExpiresOnKey = "ExpiresOn";
    internal const string RefreshTokenKey = "RefreshToken";
    internal const string RefreshTokenBase64Key = "RefreshTokenBase64";
    internal const string RefreshTokenProtectedKey = "RefreshTokenProtected";
    internal const string ClientIdKey = "ClientId";
    internal const string ClientSecretProtectedKey = "ClientSecretProtected";
    internal const string ServiceAccountJsonProtectedKey = "ServiceAccountJsonProtected";
    internal const string ServiceAccountSubjectKey = "ServiceAccountSubject";

    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    private readonly Func<OAuthCredential, Func<CancellationToken, Task<string>>?, GmailApiClient> clientFactory;
    private readonly ICredentialProtector credentialProtector;

    /// <summary>
    /// Initializes a new instance of the <see cref="GmailPendingMessageSender"/> class.
    /// </summary>
    /// <param name="clientFactory">Factory used to create <see cref="GmailApiClient"/> instances.</param>
    /// <param name="credentialProtector">Protector used to decrypt stored secrets.</param>
    public GmailPendingMessageSender(
        Func<OAuthCredential, Func<CancellationToken, Task<string>>?, GmailApiClient>? clientFactory = null,
        ICredentialProtector? credentialProtector = null) {
        this.clientFactory = clientFactory ?? ((credential, refresher) => new GmailApiClient(credential, refresher));
        this.credentialProtector = credentialProtector ?? CredentialProtection.Default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GmailPendingMessageSender"/> class.
    /// </summary>
    /// <param name="clientFactory">Factory used to create <see cref="GmailApiClient"/> instances.</param>
    [Obsolete("Use the overload accepting a refresh-aware factory.")]
    public GmailPendingMessageSender(Func<OAuthCredential, GmailApiClient> clientFactory)
        : this(
            (credential, refresher) => refresher != null
                ? new GmailApiClient(credential, refresher)
                : (clientFactory ?? throw new ArgumentNullException(nameof(clientFactory)))(credential)) {
    }

    /// <inheritdoc />
    public async Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }
        if (!record.ProviderData.TryGetValue(UserIdKey, out var userId) || string.IsNullOrWhiteSpace(userId)) {
            throw new InvalidOperationException("Pending Gmail message is missing the user identifier.");
        }

        var message = await LoadMessageAsync(record, ct).ConfigureAwait(false);
        var accessToken = ResolveAccessToken(record.ProviderData);
        var credential = BuildCredential(record.ProviderData, accessToken, userId);

        var refreshContext = BuildRefreshContext(record.ProviderData, credential);
        Func<CancellationToken, Task<string>>? refresher = null;
        if (refreshContext != null) {
            refresher = cancellationToken => RefreshAccessTokenAsync(credential, refreshContext, cancellationToken);
            if (ShouldRefreshToken(credential.ExpiresOn)) {
                await refresher(ct).ConfigureAwait(false);
            }
        } else if (ShouldRefreshToken(credential.ExpiresOn)) {
            throw new InvalidOperationException(
                "Pending Gmail message cannot be sent because the access token has expired and refresh data is unavailable.");
        }

        using var client = clientFactory(credential, refresher);
        var retryAttempted = false;
        while (true) {
            try {
                _ = await client.SendAsync(userId, message, ct).ConfigureAwait(false);
                break;
            } catch (GmailAuthenticationException ex) when (refresher == null) {
                throw new InvalidOperationException(
                    "Pending Gmail message could not be authenticated and no refresh data is available.", ex);
            } catch (GmailAuthenticationException) when (!retryAttempted && refresher != null) {
                retryAttempted = true;
                continue;
            }
        }
    }

    private static async Task<MimeMessage> LoadMessageAsync(PendingMessageRecord record, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(record.MimeMessage)) {
            throw new InvalidOperationException("Pending Gmail message does not contain MIME content.");
        }
        var bytes = Convert.FromBase64String(record.MimeMessage);
        using var stream = new MemoryStream(bytes);
        return await MimeMessage.LoadAsync(stream, ct).ConfigureAwait(false);
    }

    private static OAuthCredential BuildCredential(Dictionary<string, string> providerData, string accessToken, string userId) {
        var credential = new OAuthCredential {
            AccessToken = accessToken,
            UserName = providerData.TryGetValue(UserNameKey, out var userName) && !string.IsNullOrWhiteSpace(userName)
                ? userName
                : userId,
            ExpiresOn = ResolveExpiration(providerData),
        };

        var refreshToken = ResolveRefreshToken(providerData);
        if (!string.IsNullOrEmpty(refreshToken)) {
            credential.RefreshToken = refreshToken;
        }

        var clientId = ResolveClientId(providerData);
        if (!string.IsNullOrEmpty(clientId)) {
            credential.ClientId = clientId;
        }

        var clientSecret = ResolveProtectedString(providerData, ClientSecretProtectedKey);
        if (!string.IsNullOrEmpty(clientSecret)) {
            credential.ClientSecret = clientSecret;
        }

        var serviceAccountJson = ResolveProtectedString(providerData, ServiceAccountJsonProtectedKey);
        if (!string.IsNullOrEmpty(serviceAccountJson)) {
            credential.ServiceAccountJson = serviceAccountJson;
        }

        var subject = ResolveServiceAccountSubject(providerData);
        if (!string.IsNullOrEmpty(subject)) {
            credential.ServiceAccountSubject = subject;
        }

        return credential;
    }

    private static string ResolveAccessToken(Dictionary<string, string> providerData) {
        if (providerData.TryGetValue(AccessTokenProtectedKey, out var protectedValue) && !string.IsNullOrWhiteSpace(protectedValue)) {
            var decrypted = CredentialProtection.UnprotectWithFallback(protectedValue);
            if (!string.IsNullOrEmpty(decrypted)) {
                return decrypted;
            }
        }

        if (providerData.TryGetValue(AccessTokenBase64Key, out var encoded) && !string.IsNullOrWhiteSpace(encoded)) {
            var bytes = Convert.FromBase64String(encoded);
            return Encoding.UTF8.GetString(bytes);
        }

        if (providerData.TryGetValue(AccessTokenKey, out var token) && !string.IsNullOrWhiteSpace(token)) {
            return token;
        }

        throw new InvalidOperationException("Pending Gmail message is missing an access token.");
    }

    private static string? ResolveRefreshToken(Dictionary<string, string> providerData) {
        if (providerData.TryGetValue(RefreshTokenProtectedKey, out var protectedValue) && !string.IsNullOrWhiteSpace(protectedValue)) {
            var decrypted = CredentialProtection.UnprotectWithFallback(protectedValue);
            if (!string.IsNullOrEmpty(decrypted)) {
                return decrypted;
            }
        }

        if (providerData.TryGetValue(RefreshTokenBase64Key, out var encoded) && !string.IsNullOrWhiteSpace(encoded)) {
            var bytes = Convert.FromBase64String(encoded);
            return Encoding.UTF8.GetString(bytes);
        }

        if (providerData.TryGetValue(RefreshTokenKey, out var token) && !string.IsNullOrWhiteSpace(token)) {
            return token;
        }

        return null;
    }

    private static DateTimeOffset ResolveExpiration(Dictionary<string, string> providerData) {
        if (providerData.TryGetValue(ExpiresOnKey, out var value) && !string.IsNullOrWhiteSpace(value) &&
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)) {
            return parsed;
        }
        return DateTimeOffset.MaxValue;
    }

    private GmailRefreshContext? BuildRefreshContext(Dictionary<string, string> providerData, OAuthCredential credential) {
        var refreshToken = credential.RefreshToken;
        if (string.IsNullOrEmpty(refreshToken)) {
            return null;
        }

        credential.ClientId ??= ResolveClientId(providerData);
        credential.ClientSecret ??= ResolveProtectedString(providerData, ClientSecretProtectedKey);
        credential.ServiceAccountJson ??= ResolveProtectedString(providerData, ServiceAccountJsonProtectedKey);
        credential.ServiceAccountSubject ??= ResolveServiceAccountSubject(providerData);

        if (string.IsNullOrEmpty(credential.ClientId) && string.IsNullOrEmpty(credential.ServiceAccountJson)) {
            return null;
        }

        return new GmailRefreshContext(
            providerData,
            refreshToken,
            credential.ClientId,
            credential.ClientSecret,
            credential.ServiceAccountJson,
            credential.ServiceAccountSubject,
            credentialProtector);
    }

    private static string? ResolveClientId(Dictionary<string, string> providerData) {
        if (providerData.TryGetValue(ClientIdKey, out var value) && !string.IsNullOrWhiteSpace(value)) {
            return value;
        }
        return null;
    }

    private static string? ResolveProtectedString(Dictionary<string, string> providerData, string key) {
        if (providerData.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) {
            var decrypted = CredentialProtection.UnprotectWithFallback(value);
            return string.IsNullOrEmpty(decrypted) ? null : decrypted;
        }
        return null;
    }

    private static string? ResolveServiceAccountSubject(Dictionary<string, string> providerData) {
        if (providerData.TryGetValue(ServiceAccountSubjectKey, out var value) && !string.IsNullOrWhiteSpace(value)) {
            return value;
        }
        return null;
    }

    private static bool ShouldRefreshToken(DateTimeOffset expiresOn) => expiresOn <= DateTimeOffset.UtcNow.AddMinutes(1);

    private async Task<string> RefreshAccessTokenAsync(OAuthCredential credential, GmailRefreshContext context, CancellationToken cancellationToken) {
        if (context.HasClientSecret) {
            var refreshed = await ExchangeRefreshTokenAsync(context.ClientId!, context.ClientSecret!, context.RefreshToken, cancellationToken)
                .ConfigureAwait(false);
            credential.AccessToken = refreshed.AccessToken;
            if (!string.IsNullOrEmpty(refreshed.RefreshToken)) {
                credential.RefreshToken = refreshed.RefreshToken;
            }

            if (refreshed.ExpiresOn.HasValue) {
                credential.ExpiresOn = refreshed.ExpiresOn.Value;
            } else if (credential.ExpiresOn <= DateTimeOffset.UtcNow) {
                credential.ExpiresOn = DateTimeOffset.UtcNow.AddHours(1);
            }

            context.UpdateStoredCredential(credential);
            return credential.AccessToken;
        }

        if (context.HasServiceAccount) {
            throw new InvalidOperationException("Service account refresh is not supported for pending Gmail messages.");
        }

        throw new InvalidOperationException("Pending Gmail message is missing OAuth client context required to refresh the access token.");
    }

    private static async Task<(string AccessToken, string? RefreshToken, DateTimeOffset? ExpiresOn)> ExchangeRefreshTokenAsync(
        string clientId,
        string clientSecret,
        string refreshToken,
        CancellationToken cancellationToken) {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "refresh_token", refreshToken },
            { "grant_type", "refresh_token" }
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint) { Content = content };
        using var response = await Helpers.SharedHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!response.IsSuccessStatusCode) {
            throw new InvalidOperationException($"Failed to refresh Gmail access token: {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("access_token", out var tokenProperty)) {
            throw new InvalidOperationException("Gmail token refresh response did not include an access token.");
        }

        var accessToken = tokenProperty.GetString();
        if (string.IsNullOrEmpty(accessToken)) {
            throw new InvalidOperationException("Gmail token refresh response contained an empty access token.");
        }

        string? newRefresh = null;
        if (document.RootElement.TryGetProperty("refresh_token", out var refreshProperty)) {
            newRefresh = refreshProperty.GetString();
        }

        DateTimeOffset? expiresOn = null;
        if (document.RootElement.TryGetProperty("expires_in", out var expiresProperty) &&
            expiresProperty.TryGetInt64(out var seconds) && seconds > 0) {
            expiresOn = DateTimeOffset.UtcNow.AddSeconds(seconds);
        }

        return (accessToken, newRefresh, expiresOn);
    }

    private sealed class GmailRefreshContext {
        private readonly Dictionary<string, string> providerData;
        private readonly ICredentialProtector protector;

        internal GmailRefreshContext(
            Dictionary<string, string> providerData,
            string refreshToken,
            string? clientId,
            string? clientSecret,
            string? serviceAccountJson,
            string? serviceAccountSubject,
            ICredentialProtector protector) {
            this.providerData = providerData ?? throw new ArgumentNullException(nameof(providerData));
            RefreshToken = refreshToken ?? throw new ArgumentNullException(nameof(refreshToken));
            ClientId = clientId;
            ClientSecret = clientSecret;
            ServiceAccountJson = serviceAccountJson;
            ServiceAccountSubject = serviceAccountSubject;
            this.protector = protector ?? throw new ArgumentNullException(nameof(protector));
        }

        internal string RefreshToken { get; private set; }
        internal string? ClientId { get; }
        internal string? ClientSecret { get; }
        internal string? ServiceAccountJson { get; }
        internal string? ServiceAccountSubject { get; }

        internal bool HasClientSecret => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret);
        internal bool HasServiceAccount => !string.IsNullOrEmpty(ServiceAccountJson);

        internal void UpdateStoredCredential(OAuthCredential credential) {
            providerData[GmailPendingMessageSender.AccessTokenProtectedKey] = protector.Protect(credential.AccessToken);
            providerData.Remove(GmailPendingMessageSender.AccessTokenBase64Key);
            providerData.Remove(GmailPendingMessageSender.AccessTokenKey);

            if (!string.IsNullOrEmpty(credential.RefreshToken)) {
                providerData[GmailPendingMessageSender.RefreshTokenProtectedKey] = protector.Protect(credential.RefreshToken);
                providerData.Remove(GmailPendingMessageSender.RefreshTokenBase64Key);
                providerData.Remove(GmailPendingMessageSender.RefreshTokenKey);
                RefreshToken = credential.RefreshToken!;
            }

            providerData[GmailPendingMessageSender.ExpiresOnKey] = credential.ExpiresOn.ToString("o", CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(credential.ClientId)) {
                providerData[GmailPendingMessageSender.ClientIdKey] = credential.ClientId!;
            }

            if (!string.IsNullOrEmpty(credential.ClientSecret)) {
                providerData[GmailPendingMessageSender.ClientSecretProtectedKey] = protector.Protect(credential.ClientSecret!);
            }

            if (!string.IsNullOrEmpty(credential.ServiceAccountJson)) {
                providerData[GmailPendingMessageSender.ServiceAccountJsonProtectedKey] = protector.Protect(credential.ServiceAccountJson!);
            }

            if (!string.IsNullOrEmpty(credential.ServiceAccountSubject)) {
                providerData[GmailPendingMessageSender.ServiceAccountSubjectKey] = credential.ServiceAccountSubject!;
            }
        }
    }
}
