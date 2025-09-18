using System.Globalization;
using System.Text;

namespace Mailozaurr;

/// <summary>
/// Sends queued Gmail messages using <see cref="GmailApiClient"/>.
/// </summary>
public sealed class GmailPendingMessageSender : IPendingMessageSender {
    internal const string UserIdKey = "UserId";
    internal const string AccessTokenKey = "AccessToken";
    internal const string AccessTokenBase64Key = "AccessTokenBase64";
    internal const string UserNameKey = "UserName";
    internal const string ExpiresOnKey = "ExpiresOn";
    internal const string RefreshTokenKey = "RefreshToken";
    internal const string RefreshTokenBase64Key = "RefreshTokenBase64";

    private readonly Func<OAuthCredential, GmailApiClient> clientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="GmailPendingMessageSender"/> class.
    /// </summary>
    /// <param name="clientFactory">Factory used to create <see cref="GmailApiClient"/> instances.</param>
    public GmailPendingMessageSender(Func<OAuthCredential, GmailApiClient>? clientFactory = null) {
        this.clientFactory = clientFactory ?? (credential => new GmailApiClient(credential));
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
        using var client = clientFactory(credential);
        _ = await client.SendAsync(userId, message, ct).ConfigureAwait(false);
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
        return credential;
    }

    private static string ResolveAccessToken(Dictionary<string, string> providerData) {
        if (providerData.TryGetValue(AccessTokenBase64Key, out var encoded) && !string.IsNullOrWhiteSpace(encoded)) {
            var unprotectedToken = CredentialProtection.UnprotectWithFallback(encoded);
            if (!string.IsNullOrEmpty(unprotectedToken)) {
                return unprotectedToken;
            }
        }

        if (providerData.TryGetValue(AccessTokenKey, out var tokenValue) && !string.IsNullOrWhiteSpace(tokenValue)) {
            return tokenValue;
        }

        throw new InvalidOperationException("Pending Gmail message is missing an access token.");
    }

    private static string? ResolveRefreshToken(Dictionary<string, string> providerData) {
        if (providerData.TryGetValue(RefreshTokenBase64Key, out var encoded) && !string.IsNullOrWhiteSpace(encoded)) {
            var unprotectedToken = CredentialProtection.UnprotectWithFallback(encoded);
            if (!string.IsNullOrEmpty(unprotectedToken)) {
                return unprotectedToken;
            }
        }

        if (providerData.TryGetValue(RefreshTokenKey, out var tokenValue) && !string.IsNullOrWhiteSpace(tokenValue)) {
            return tokenValue;
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
}
