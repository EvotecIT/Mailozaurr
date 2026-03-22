using System.Globalization;
using System.IO;
using System.Text;
using MimeKit;

namespace Mailozaurr;

/// <summary>
/// Sends queued Microsoft Graph messages using <see cref="GraphApiClient"/>.
/// </summary>
public sealed class GraphPendingMessageSender : IPendingMessageSender {
    /// <summary>Provider-data key storing the Graph mailbox user id.</summary>
    public const string UserIdKey = "UserId";

    /// <summary>Provider-data key storing the Graph mailbox user name.</summary>
    public const string UserNameKey = "UserName";

    /// <summary>Provider-data key storing the access-token expiry timestamp.</summary>
    public const string ExpiresOnKey = "ExpiresOn";

    /// <summary>Provider-data key storing the raw access token.</summary>
    public const string AccessTokenKey = "AccessToken";

    /// <summary>Provider-data key storing the base64-encoded access token.</summary>
    public const string AccessTokenBase64Key = "AccessTokenBase64";

    /// <summary>Provider-data key storing the protected access token.</summary>
    public const string AccessTokenProtectedKey = "AccessTokenProtected";

    /// <summary>Provider-data key storing the Graph client id.</summary>
    public const string ClientIdKey = "ClientId";

    /// <summary>Provider-data key storing the Graph tenant id.</summary>
    public const string TenantIdKey = "TenantId";

    /// <summary>Provider-data key storing the protected Graph client secret.</summary>
    public const string ClientSecretProtectedKey = "ClientSecretProtected";

    /// <summary>Provider-data key storing the Graph certificate path.</summary>
    public const string CertificatePathKey = "CertificatePath";

    /// <summary>Provider-data key storing the protected Graph certificate password.</summary>
    public const string CertificatePasswordProtectedKey = "CertificatePasswordProtected";

    private readonly Func<OAuthCredential, GraphApiClient> clientFactory;
    private readonly Func<GraphCredential, CancellationToken, Task<string>> acquireAccessTokenAsync;

    /// <summary>
    /// Creates a new pending-message sender for Graph.
    /// </summary>
    public GraphPendingMessageSender(
        Func<OAuthCredential, GraphApiClient>? clientFactory = null,
        Func<GraphCredential, CancellationToken, Task<string>>? acquireAccessTokenAsync = null) {
        this.clientFactory = clientFactory ?? (credential => new GraphApiClient(credential));
        this.acquireAccessTokenAsync = acquireAccessTokenAsync ?? DefaultAcquireAccessTokenAsync;
    }

    /// <inheritdoc />
    public async Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }
        if (!record.ProviderData.TryGetValue(UserIdKey, out var userId) || string.IsNullOrWhiteSpace(userId)) {
            throw new InvalidOperationException("Pending Graph message is missing the user identifier.");
        }

        var message = await LoadMessageAsync(record, ct).ConfigureAwait(false);
        var credential = await ResolveCredentialAsync(record.ProviderData, userId, ct).ConfigureAwait(false);

        using var client = clientFactory(credential);
        await GraphMimeMessageSender.SendAsync(client, userId, message, ct).ConfigureAwait(false);
    }

    private async Task<OAuthCredential> ResolveCredentialAsync(
        Dictionary<string, string> providerData,
        string userId,
        CancellationToken cancellationToken) {
        var accessToken = ResolveAccessToken(providerData);
        var expiresOn = ResolveExpiration(providerData);
        var graphCredential = BuildGraphCredential(providerData);

        if ((string.IsNullOrEmpty(accessToken) || ShouldRefreshToken(expiresOn)) && graphCredential != null) {
            accessToken = await acquireAccessTokenAsync(graphCredential, cancellationToken).ConfigureAwait(false);
            expiresOn = DateTimeOffset.UtcNow.AddMinutes(55);
            providerData[AccessTokenProtectedKey] = CredentialProtection.Default.Protect(accessToken);
            providerData.Remove(AccessTokenKey);
            providerData.Remove(AccessTokenBase64Key);
            providerData[ExpiresOnKey] = expiresOn.ToString("o", CultureInfo.InvariantCulture);
        }

        if (string.IsNullOrEmpty(accessToken)) {
            throw new InvalidOperationException("Pending Graph message is missing an access token and cannot mint a new one.");
        }

        return new OAuthCredential {
            UserName = providerData.TryGetValue(UserNameKey, out var userName) && !string.IsNullOrWhiteSpace(userName)
                ? userName
                : userId,
            AccessToken = accessToken!,
            ExpiresOn = expiresOn,
            ClientId = providerData.TryGetValue(ClientIdKey, out var clientId) && !string.IsNullOrWhiteSpace(clientId)
                ? clientId
                : null,
            ClientSecret = ResolveProtectedString(providerData, ClientSecretProtectedKey)
        };
    }

    private static async Task<MimeMessage> LoadMessageAsync(PendingMessageRecord record, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(record.MimeMessage)) {
            throw new InvalidOperationException("Pending Graph message does not contain MIME content.");
        }

        var bytes = Convert.FromBase64String(record.MimeMessage);
        using var stream = new MemoryStream(bytes);
        return await MimeMessage.LoadAsync(stream, ct).ConfigureAwait(false);
    }

    private static string? ResolveAccessToken(Dictionary<string, string> providerData) {
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

        return null;
    }

    private static DateTimeOffset ResolveExpiration(Dictionary<string, string> providerData) {
        if (providerData.TryGetValue(ExpiresOnKey, out var value) && !string.IsNullOrWhiteSpace(value) &&
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)) {
            return parsed;
        }

        return DateTimeOffset.MaxValue;
    }

    private static GraphCredential? BuildGraphCredential(Dictionary<string, string> providerData) {
        if (!providerData.TryGetValue(ClientIdKey, out var clientId) || string.IsNullOrWhiteSpace(clientId) ||
            !providerData.TryGetValue(TenantIdKey, out var tenantId) || string.IsNullOrWhiteSpace(tenantId)) {
            return null;
        }

        var clientSecret = ResolveProtectedString(providerData, ClientSecretProtectedKey);
        providerData.TryGetValue(CertificatePathKey, out var certificatePath);
        var certificatePassword = ResolveProtectedString(providerData, CertificatePasswordProtectedKey);

        if (string.IsNullOrWhiteSpace(clientSecret) && string.IsNullOrWhiteSpace(certificatePath)) {
            return null;
        }

        return new GraphCredential {
            ClientId = clientId.Trim(),
            DirectoryId = tenantId.Trim(),
            ClientSecret = string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret,
            CertificatePath = string.IsNullOrWhiteSpace(certificatePath) ? null : certificatePath.Trim(),
            CertificatePassword = string.IsNullOrWhiteSpace(certificatePassword) ? null : certificatePassword
        };
    }

    private static string? ResolveProtectedString(Dictionary<string, string> providerData, string key) {
        if (providerData.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) {
            var decrypted = CredentialProtection.UnprotectWithFallback(value);
            return string.IsNullOrEmpty(decrypted) ? null : decrypted;
        }

        return null;
    }

    private static bool ShouldRefreshToken(DateTimeOffset expiresOn) => expiresOn <= DateTimeOffset.UtcNow.AddMinutes(1);

    private static async Task<string> DefaultAcquireAccessTokenAsync(GraphCredential credential, CancellationToken cancellationToken) {
        var authorization = await MicrosoftGraphUtils.ConnectO365GraphAsync(
            credential,
            credential.DirectoryId,
            "https://graph.microsoft.com",
            cancellationToken).ConfigureAwait(false);
        return NormalizeAccessToken(authorization);
    }

    private static string NormalizeAccessToken(string authorizationHeaderValue) {
        if (string.IsNullOrWhiteSpace(authorizationHeaderValue)) {
            throw new InvalidOperationException("Graph authorization did not return a token.");
        }

        var trimmed = authorizationHeaderValue.Trim();
        const string bearerPrefix = "Bearer ";
        return trimmed.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed.Substring(bearerPrefix.Length).Trim()
            : trimmed;
    }
}
