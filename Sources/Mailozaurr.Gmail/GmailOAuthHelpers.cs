using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using System.Security.Cryptography;
using System.Text;

namespace Mailozaurr;

/// <summary>Acquires and caches Google OAuth credentials used by Gmail transports.</summary>
public static class GmailOAuthHelpers {
    private static string BuildLegacyCacheKey(string gmailAccount) => $"google:{gmailAccount}";

    private static string BuildClientCacheKey(string gmailAccount, string clientId) =>
        $"google:{clientId}:{gmailAccount}";

    private static string[] NormalizeScopes(IEnumerable<string> scopes) =>
        scopes?
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(scope => scope, StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? Array.Empty<string>();

    private static string BuildScopeFingerprint(IEnumerable<string> scopes) {
        var normalized = NormalizeScopes(scopes);
        var source = normalized.Length == 0 ? "default" : string.Join("\n", normalized);
        using var sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(source)))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static string BuildCacheKey(string gmailAccount, string clientId, IEnumerable<string> scopes) =>
        $"google:{clientId}:{gmailAccount}:scopes:{BuildScopeFingerprint(scopes)}";

    private static async Task PersistCredentialAsync(
        OAuthCredential credential, string gmailAccount, string clientId, IEnumerable<string> scopes) {
        if (credential == null) throw new ArgumentNullException(nameof(credential));
        if (string.IsNullOrWhiteSpace(gmailAccount) && string.IsNullOrWhiteSpace(credential.UserName)) return;
        string account = string.IsNullOrWhiteSpace(gmailAccount) ? credential.UserName : gmailAccount.Trim();
        var normalizedScopes = NormalizeScopes(scopes);
        credential.ClientId = clientId;
        await OAuthTokenCache.SetAsync(BuildCacheKey(account, clientId, normalizedScopes), credential).ConfigureAwait(false);
        if (normalizedScopes.Length == 0) {
            await OAuthTokenCache.SetAsync(BuildLegacyCacheKey(account), credential).ConfigureAwait(false);
            await OAuthTokenCache.SetAsync(BuildClientCacheKey(account, clientId), credential).ConfigureAwait(false);
        }
    }

    private static Task PersistCredentialAsync(OAuthCredential credential, string gmailAccount, string clientId) =>
        PersistCredentialAsync(credential, gmailAccount, clientId, Array.Empty<string>());

    /// <summary>Acquires a Google OAuth token using the installed-application browser flow.</summary>
    public static async Task<OAuthCredential> AcquireTokenInteractiveAsync(
        string gmailAccount, string clientId, string clientSecret, IEnumerable<string> scopes) {
        var normalizedScopes = NormalizeScopes(scopes);
        var clientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };
        var initializer = new GoogleAuthorizationCodeFlow.Initializer {
            ClientSecrets = clientSecrets,
            Scopes = normalizedScopes,
            DataStore = new FileDataStore("CredentialCacheFolder-" + BuildScopeFingerprint(normalizedScopes), false)
        };
        var codeFlow = new GoogleAuthorizationCodeFlow(initializer);
        var authCode = new AuthorizationCodeInstalledApp(codeFlow, new LocalServerCodeReceiver());
        UserCredential credential = await authCode.AuthorizeAsync(gmailAccount, CancellationToken.None);
        if (credential.Token.IsStale) await credential.RefreshTokenAsync(CancellationToken.None);
        var result = new OAuthCredential {
            UserName = gmailAccount.Trim(),
            AccessToken = credential.Token.AccessToken,
            RefreshToken = credential.Token.RefreshToken,
            ExpiresOn = credential.Token.IssuedUtc + TimeSpan.FromSeconds(credential.Token.ExpiresInSeconds ?? 0),
            ClientId = clientId,
            ClientSecret = clientSecret
        };
        await PersistCredentialAsync(result, gmailAccount, clientId, normalizedScopes).ConfigureAwait(false);
        return result;
    }

    /// <summary>Returns a compatible cached Google credential or acquires a new one.</summary>
    public static async Task<OAuthCredential> AcquireTokenCachedAsync(
        string gmailAccount, string clientId, string clientSecret, IEnumerable<string> scopes) {
        var normalizedScopes = NormalizeScopes(scopes);
        string scopedKey = BuildCacheKey(gmailAccount, clientId, normalizedScopes);
        string compositeKey = BuildClientCacheKey(gmailAccount, clientId);
        string legacyKey = BuildLegacyCacheKey(gmailAccount);
        bool loadedFromLegacy = false;
        OAuthCredential? cached = await OAuthTokenCache.GetAsync(scopedKey).ConfigureAwait(false);
        if (cached is null && normalizedScopes.Length == 0) {
            cached = await OAuthTokenCache.GetAsync(compositeKey).ConfigureAwait(false);
        }
        if (cached is null && normalizedScopes.Length == 0) {
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
            if (loadedFromLegacy) await PersistCredentialAsync(cached, gmailAccount, clientId, normalizedScopes).ConfigureAwait(false);
            return cached;
        }
        if (cached != null && !string.IsNullOrWhiteSpace(cached.RefreshToken)) {
            var initializer = new GoogleAuthorizationCodeFlow.Initializer {
                ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
                Scopes = normalizedScopes,
                DataStore = new FileDataStore("CredentialCacheFolder-" + BuildScopeFingerprint(normalizedScopes), false)
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
                await PersistCredentialAsync(refreshed, gmailAccount, clientId, normalizedScopes).ConfigureAwait(false);
                return refreshed;
            }
        }
        return await AcquireTokenInteractiveAsync(gmailAccount, clientId, clientSecret, normalizedScopes)
            .ConfigureAwait(false);
    }
}
