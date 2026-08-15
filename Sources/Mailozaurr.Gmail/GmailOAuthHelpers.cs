using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;

namespace Mailozaurr;

/// <summary>Acquires and caches Google OAuth credentials used by Gmail transports.</summary>
public static class GmailOAuthHelpers {
    private static string BuildLegacyCacheKey(string gmailAccount) => $"google:{gmailAccount}";

    private static string BuildCacheKey(string gmailAccount, string clientId) =>
        $"google:{clientId}:{gmailAccount}";

    private static async Task PersistCredentialAsync(
        OAuthCredential credential, string gmailAccount, string clientId) {
        if (credential == null) throw new ArgumentNullException(nameof(credential));
        if (string.IsNullOrWhiteSpace(gmailAccount) && string.IsNullOrWhiteSpace(credential.UserName)) return;
        string account = string.IsNullOrWhiteSpace(gmailAccount) ? credential.UserName : gmailAccount.Trim();
        credential.ClientId = clientId;
        await OAuthTokenCache.SetAsync(BuildLegacyCacheKey(account), credential).ConfigureAwait(false);
        await OAuthTokenCache.SetAsync(BuildCacheKey(account, clientId), credential).ConfigureAwait(false);
    }

    /// <summary>Acquires a Google OAuth token using the installed-application browser flow.</summary>
    public static async Task<OAuthCredential> AcquireTokenInteractiveAsync(
        string gmailAccount, string clientId, string clientSecret, IEnumerable<string> scopes) {
        var clientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };
        var initializer = new GoogleAuthorizationCodeFlow.Initializer {
            ClientSecrets = clientSecrets,
            Scopes = scopes,
            DataStore = new FileDataStore("CredentialCacheFolder", false)
        };
        var codeFlow = new GoogleAuthorizationCodeFlow(initializer);
        var authCode = new AuthorizationCodeInstalledApp(codeFlow, new LocalServerCodeReceiver());
        UserCredential credential = await authCode.AuthorizeAsync(gmailAccount, CancellationToken.None);
        if (credential.Token.IsStale) await credential.RefreshTokenAsync(CancellationToken.None);
        var result = new OAuthCredential {
            UserName = credential.UserId,
            AccessToken = credential.Token.AccessToken,
            RefreshToken = credential.Token.RefreshToken,
            ExpiresOn = credential.Token.IssuedUtc + TimeSpan.FromSeconds(credential.Token.ExpiresInSeconds ?? 0),
            ClientId = clientId,
            ClientSecret = clientSecret
        };
        await PersistCredentialAsync(result, gmailAccount, clientId).ConfigureAwait(false);
        return result;
    }

    /// <summary>Returns a compatible cached Google credential or acquires a new one.</summary>
    public static async Task<OAuthCredential> AcquireTokenCachedAsync(
        string gmailAccount, string clientId, string clientSecret, IEnumerable<string> scopes) {
        string compositeKey = BuildCacheKey(gmailAccount, clientId);
        string legacyKey = BuildLegacyCacheKey(gmailAccount);
        bool loadedFromLegacy = false;
        OAuthCredential? cached = await OAuthTokenCache.GetAsync(compositeKey).ConfigureAwait(false);
        if (cached is null) {
            cached = await OAuthTokenCache.GetAsync(legacyKey).ConfigureAwait(false);
            loadedFromLegacy = cached != null;
        }
        if (cached != null) {
            if (!string.IsNullOrEmpty(cached.ClientId) &&
                !string.Equals(cached.ClientId, clientId, StringComparison.Ordinal)) {
                LoggingMessages.Logger.WriteWarning(
                    "OAuth cache entry for {0} was created with a different ClientId. Ignoring cached token.", gmailAccount);
                cached = null;
            } else {
                cached.ClientId ??= clientId;
                if (string.IsNullOrEmpty(cached.ClientSecret)) cached.ClientSecret = clientSecret;
                else if (!string.Equals(cached.ClientSecret, clientSecret, StringComparison.Ordinal)) {
                    LoggingMessages.Logger.WriteWarning(
                        "OAuth cache entry for {0} contains a different ClientSecret than provided. Proceeding to refresh with provided secret.", gmailAccount);
                }
            }
        }
        if (cached != null && cached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5)) {
            if (loadedFromLegacy) await PersistCredentialAsync(cached, gmailAccount, clientId).ConfigureAwait(false);
            return cached;
        }
        if (cached != null && !string.IsNullOrWhiteSpace(cached.RefreshToken)) {
            var initializer = new GoogleAuthorizationCodeFlow.Initializer {
                ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
                Scopes = scopes,
                DataStore = new FileDataStore("CredentialCacheFolder", false)
            };
            var flow = new GoogleAuthorizationCodeFlow(initializer);
            var userCredential = new UserCredential(flow, gmailAccount,
                new TokenResponse { RefreshToken = cached.RefreshToken });
            if (await userCredential.RefreshTokenAsync(CancellationToken.None).ConfigureAwait(false)) {
                var refreshed = new OAuthCredential {
                    UserName = gmailAccount,
                    AccessToken = userCredential.Token.AccessToken,
                    RefreshToken = userCredential.Token.RefreshToken,
                    ExpiresOn = userCredential.Token.IssuedUtc +
                                TimeSpan.FromSeconds(userCredential.Token.ExpiresInSeconds ?? 0),
                    ClientId = clientId,
                    ClientSecret = clientSecret
                };
                await PersistCredentialAsync(refreshed, gmailAccount, clientId).ConfigureAwait(false);
                return refreshed;
            }
        }
        return await AcquireTokenInteractiveAsync(gmailAccount, clientId, clientSecret, scopes)
            .ConfigureAwait(false);
    }
}
