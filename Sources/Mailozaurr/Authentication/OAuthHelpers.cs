using Microsoft.Identity.Client;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;
using Google.Apis.Auth.OAuth2.Responses;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using System;
using System.Collections.Generic;

namespace Mailozaurr;

/// <summary>
/// Helper methods for acquiring OAuth tokens for various services.
/// </summary>
public static class OAuthHelpers {
    private static string[] NormalizeScopes(IEnumerable<string> scopes) =>
        scopes?
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(scope => scope, StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? Array.Empty<string>();

    private static string? BuildO365CacheKey(
        string? login,
        string clientId,
        string tenantId,
        string redirectUri,
        IEnumerable<string> scopes) {
        if (string.IsNullOrWhiteSpace(login)) {
            return null;
        }

        var normalizedScopes = NormalizeScopes(scopes);
        var scopeKey = normalizedScopes.Length == 0 ? "default" : string.Join(" ", normalizedScopes);
        return $"o365:{login!.Trim()}|{clientId.Trim()}|{tenantId.Trim()}|{redirectUri.Trim()}|{scopeKey}";
    }

    /// <summary>
    /// Acquires an OAuth token for Office 365 using an interactive browser flow.
    /// </summary>
    /// <param name="login">Optional login hint for the account.</param>
    /// <param name="clientId">The application (client) identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="redirectUri">The redirect URI registered for the application.</param>
    /// <param name="scopes">The scopes to request for the token.</param>
    /// <returns>A credential containing the access token.</returns>
    public static async Task<OAuthCredential> AcquireO365TokenInteractiveAsync(
        string? login,
        string clientId,
        string tenantId,
        string redirectUri,
        IEnumerable<string> scopes) {
        var options = new PublicClientApplicationOptions {
            ClientId = clientId,
            TenantId = tenantId,
            RedirectUri = redirectUri
        };
        var app = PublicClientApplicationBuilder.CreateWithApplicationOptions(options).Build();
        TokenCacheHelper.RegisterCache(app.UserTokenCache);
        AuthenticationResult result;
        var accounts = await app.GetAccountsAsync();
        IAccount? account = null;
        if (!string.IsNullOrWhiteSpace(login)) {
            account = accounts.FirstOrDefault(a => string.Equals(a.Username, login, StringComparison.OrdinalIgnoreCase));
        } else {
            account = accounts.FirstOrDefault();
        }
        try {
            if (account != null) {
                result = await app.AcquireTokenSilent(scopes, account).ExecuteAsync();
            } else {
                throw new MsalUiRequiredException("", "no_account");
            }
        } catch (MsalUiRequiredException) {
            var builder = app.AcquireTokenInteractive(scopes);
            if (!string.IsNullOrWhiteSpace(login)) {
                builder = builder.WithLoginHint(login);
            }
            result = await builder.ExecuteAsync();
        }
        var cred = new OAuthCredential {
            UserName = result.Account.Username,
            AccessToken = result.AccessToken,
            ExpiresOn = result.ExpiresOn,
            ClientId = clientId
        };
        await OAuthTokenCache.SetAsync($"o365:{cred.UserName}", cred);
        return cred;
    }

    /// <summary>
    /// Attempts to silently acquire a new Office 365 access token using the cached refresh token.
    /// </summary>
    private static async Task<OAuthCredential?> AcquireO365TokenSilentAsync(
        string? login,
        string clientId,
        string tenantId,
        string redirectUri,
        IEnumerable<string> scopes) {
        var options = new PublicClientApplicationOptions {
            ClientId = clientId,
            TenantId = tenantId,
            RedirectUri = redirectUri
        };
        var app = PublicClientApplicationBuilder.CreateWithApplicationOptions(options).Build();
        TokenCacheHelper.RegisterCache(app.UserTokenCache);
        var accounts = await app.GetAccountsAsync();
        IAccount? account = null;
        if (!string.IsNullOrWhiteSpace(login)) {
            account = accounts.FirstOrDefault(a => string.Equals(a.Username, login, StringComparison.OrdinalIgnoreCase));
        } else {
            account = accounts.FirstOrDefault();
        }
        if (account == null) {
            return null;
        }
        try {
            var result = await app.AcquireTokenSilent(scopes, account).ExecuteAsync();
            return new OAuthCredential {
                UserName = result.Account.Username,
                AccessToken = result.AccessToken,
                ExpiresOn = result.ExpiresOn,
                ClientId = clientId
            };
        } catch (MsalUiRequiredException) {
            return null;
        }
    }

    /// <summary>
    /// Acquires an OAuth token for a Gmail account using an interactive browser flow.
    /// </summary>
    /// <param name="gmailAccount">The Gmail account to authenticate.</param>
    /// <param name="clientId">The OAuth client identifier.</param>
    /// <param name="clientSecret">The OAuth client secret.</param>
    /// <param name="scopes">The scopes to request for the token.</param>
    /// <returns>A credential containing the access token.</returns>
    public static async Task<OAuthCredential> AcquireGoogleTokenInteractiveAsync(
        string gmailAccount,
        string clientId,
        string clientSecret,
        IEnumerable<string> scopes) {
        var clientSecrets = new ClientSecrets {
            ClientId = clientId,
            ClientSecret = clientSecret
        };
        var initializer = new GoogleAuthorizationCodeFlow.Initializer {
            ClientSecrets = clientSecrets,
            Scopes = scopes,
            DataStore = new FileDataStore("CredentialCacheFolder", false)
        };
        var codeFlow = new GoogleAuthorizationCodeFlow(initializer);
        var codeReceiver = new LocalServerCodeReceiver();
        var authCode = new AuthorizationCodeInstalledApp(codeFlow, codeReceiver);
        var credential = await authCode.AuthorizeAsync(gmailAccount, System.Threading.CancellationToken.None);
        if (credential.Token.IsStale) {
            await credential.RefreshTokenAsync(System.Threading.CancellationToken.None);
        }
        var cred = new OAuthCredential {
            UserName = credential.UserId,
            AccessToken = credential.Token.AccessToken,
            RefreshToken = credential.Token.RefreshToken,
            ExpiresOn = credential.Token.IssuedUtc + TimeSpan.FromSeconds(credential.Token.ExpiresInSeconds ?? 0),
            ClientId = clientId,
            ClientSecret = clientSecret
        };
        await OAuthTokenCache.SetAsync($"google:{cred.UserName}", cred);
        return cred;
    }

    /// <summary>
    /// Attempts to retrieve a cached Office 365 token or acquire a new one if necessary.
    /// </summary>
    public static async Task<OAuthCredential> AcquireO365TokenCachedAsync(
        string? login,
        string clientId,
        string tenantId,
        string redirectUri,
        IEnumerable<string> scopes) {
        var normalizedScopes = NormalizeScopes(scopes);
        var cacheKey = BuildO365CacheKey(login, clientId, tenantId, redirectUri, normalizedScopes);
        if (cacheKey != null) {
            var cached = await OAuthTokenCache.GetAsync(cacheKey);
            if (cached != null && cached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5)) {
                return cached;
            }
            if (cached != null) {
                var refreshed = await AcquireO365TokenSilentAsync(login, clientId, tenantId, redirectUri, normalizedScopes);
                if (refreshed != null) {
                    await OAuthTokenCache.SetAsync(cacheKey, refreshed);
                    return refreshed;
                }
            }
        }

        var cred = await AcquireO365TokenInteractiveAsync(login, clientId, tenantId, redirectUri, normalizedScopes);
        if (cacheKey != null) {
            await OAuthTokenCache.SetAsync(cacheKey, cred);
        }
        return cred;
    }

    /// <summary>
    /// Attempts to retrieve a cached Google token or acquire a new one if necessary.
    /// </summary>
    public static async Task<OAuthCredential> AcquireGoogleTokenCachedAsync(
        string gmailAccount,
        string clientId,
        string clientSecret,
        IEnumerable<string> scopes) {
        // Prefer a cache key that includes client id to avoid token confusion across apps.
        var compositeKey = $"google:{clientId}:{gmailAccount}";
        var legacyKey = $"google:{gmailAccount}";

        bool loadedFromLegacy = false;
        var cached = await OAuthTokenCache.GetAsync(compositeKey).ConfigureAwait(false);
        if (cached is null) {
            cached = await OAuthTokenCache.GetAsync(legacyKey).ConfigureAwait(false);
            loadedFromLegacy = cached != null;
        }

        if (cached != null) {
            // Validate that cached token belongs to the same app/client.
            if (!string.IsNullOrEmpty(cached.ClientId) && !string.Equals(cached.ClientId, clientId, StringComparison.Ordinal)) {
                LoggingMessages.Logger.WriteWarning("OAuth cache entry for {0} was created with a different ClientId. Ignoring cached token.", gmailAccount);
                cached = null;
            } else {
                // Fill blanks, but do not override mismatched values.
                cached.ClientId ??= clientId;
                if (string.IsNullOrEmpty(cached.ClientSecret)) {
                    cached.ClientSecret = clientSecret;
                } else if (!string.Equals(cached.ClientSecret, clientSecret, StringComparison.Ordinal)) {
                    LoggingMessages.Logger.WriteWarning("OAuth cache entry for {0} contains a different ClientSecret than provided. Proceeding to refresh with provided secret.", gmailAccount);
                }
            }
        }

        if (cached != null && cached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5)) {
            // Migrate legacy entries to composite key to prevent cross-app confusion.
            if (loadedFromLegacy) {
                await OAuthTokenCache.SetAsync(compositeKey, cached).ConfigureAwait(false);
            }
            return cached;
        }

        if (cached != null && !string.IsNullOrWhiteSpace(cached.RefreshToken)) {
            var clientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };
            var initializer = new GoogleAuthorizationCodeFlow.Initializer {
                ClientSecrets = clientSecrets,
                Scopes = scopes,
                DataStore = new FileDataStore("CredentialCacheFolder", false)
            };
            var flow = new GoogleAuthorizationCodeFlow(initializer);
            var token = new Google.Apis.Auth.OAuth2.Responses.TokenResponse { RefreshToken = cached.RefreshToken };
            var userCred = new UserCredential(flow, gmailAccount, token);
            var refreshed = await userCred.RefreshTokenAsync(System.Threading.CancellationToken.None);
            if (refreshed) {
                var newCred = new OAuthCredential {
                    UserName = gmailAccount,
                    AccessToken = userCred.Token.AccessToken,
                    RefreshToken = userCred.Token.RefreshToken,
                    ExpiresOn = userCred.Token.IssuedUtc + TimeSpan.FromSeconds(userCred.Token.ExpiresInSeconds ?? 0),
                    ClientId = clientId,
                    ClientSecret = clientSecret
                };
                await OAuthTokenCache.SetAsync(compositeKey, newCred).ConfigureAwait(false);
                return newCred;
            }
        }

        var cred = await AcquireGoogleTokenInteractiveAsync(gmailAccount, clientId, clientSecret, scopes).ConfigureAwait(false);
        await OAuthTokenCache.SetAsync(compositeKey, cred).ConfigureAwait(false);
        return cred;
    }

    /// <summary>
    /// Acquires an Office 365 access token using the device code flow.
    /// </summary>
    /// <param name="clientId">The application (client) identifier.</param>
    /// <param name="tenantId">The tenant (directory) identifier.</param>
    /// <param name="scopes">Scopes to request for the token.</param>
    /// <param name="deviceCodeCallback">Optional callback to display the device code.</param>
    /// <returns>The acquired credential.</returns>
    public static async Task<OAuthCredential> AcquireO365TokenDeviceCodeAsync(
        string clientId,
        string tenantId,
        IEnumerable<string> scopes,
        Func<DeviceCodeResult, Task>? deviceCodeCallback = null) {
        var options = new PublicClientApplicationOptions {
            ClientId = clientId,
            TenantId = tenantId
        };
        var app = PublicClientApplicationBuilder.CreateWithApplicationOptions(options).Build();
        TokenCacheHelper.RegisterCache(app.UserTokenCache);
        deviceCodeCallback ??= result => {
            Console.WriteLine(result.Message);
            return Task.CompletedTask;
        };
        var result = await app.AcquireTokenWithDeviceCode(scopes, deviceCodeCallback).ExecuteAsync();
        var cred = new OAuthCredential {
            UserName = result.Account.Username,
            AccessToken = result.AccessToken,
            ExpiresOn = result.ExpiresOn
        };
        await OAuthTokenCache.SetAsync($"o365:{cred.UserName}", cred);
        return cred;
    }

    /// <summary>
    /// Acquires an Office 365 access token on behalf of a user using an existing token.
    /// </summary>
    /// <param name="clientId">The application (client) identifier.</param>
    /// <param name="tenantId">The tenant (directory) identifier.</param>
    /// <param name="clientSecret">The client secret for the application.</param>
    /// <param name="userAccessToken">The user access token to exchange.</param>
    /// <param name="scopes">Scopes to request for the new token.</param>
    /// <returns>The acquired credential.</returns>
    public static async Task<OAuthCredential> AcquireO365TokenOnBehalfOfAsync(
        string clientId,
        string tenantId,
        string clientSecret,
        string userAccessToken,
        IEnumerable<string> scopes) {
        var app = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithTenantId(tenantId)
            .WithClientSecret(clientSecret)
            .Build();
        var assertion = new UserAssertion(userAccessToken);
        var result = await app.AcquireTokenOnBehalfOf(scopes, assertion).ExecuteAsync();
        return new OAuthCredential {
            UserName = result.Account.Username,
            AccessToken = result.AccessToken,
            ExpiresOn = result.ExpiresOn
        };
    }

    /// <summary>
    /// Acquires an app-only Microsoft Graph token using a certificate.
    /// </summary>
    /// <param name="clientId">The application (client) identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="certificatePath">Path to the certificate file (PFX).</param>
    /// <param name="certificatePassword">Password for the certificate.</param>
    /// <param name="scopes">Optional scopes to request.</param>
    /// <returns>The authorization information including access token.</returns>
    public static async Task<GraphAuthorization> AcquireGraphCertificateTokenAsync(
        string clientId,
        string tenantId,
        string certificatePath,
        string certificatePassword,
        IEnumerable<string>? scopes = null) {
        var certificate = new X509Certificate2(certificatePath, certificatePassword);
        return await AcquireGraphCertificateTokenInternal(clientId, tenantId, certificate, scopes);
    }

    /// <summary>
    /// Acquires an app-only token using a certificate provided as a byte array.
    /// </summary>
    /// <param name="clientId">The application (client) identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="certificateBytes">Certificate bytes in PFX format.</param>
    /// <param name="certificatePassword">Password for the certificate.</param>
    /// <param name="scopes">Optional scopes to request.</param>
    /// <returns>The authorization information including access token.</returns>
    public static async Task<GraphAuthorization> AcquireGraphCertificateTokenAsync(
        string clientId,
        string tenantId,
        byte[] certificateBytes,
        string certificatePassword,
        IEnumerable<string>? scopes = null) {
        var certificate = new X509Certificate2(certificateBytes, certificatePassword);
        return await AcquireGraphCertificateTokenInternal(clientId, tenantId, certificate, scopes);
    }

    /// <summary>
    /// Acquires an app-only token using a PEM encoded certificate file.
    /// </summary>
    /// <param name="clientId">The application (client) identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="pemPath">Path to the PEM certificate file.</param>
    /// <param name="scopes">Optional scopes to request.</param>
    /// <returns>The authorization information including access token.</returns>
    public static Task<GraphAuthorization> AcquireGraphCertificatePemTokenAsync(
        string clientId,
        string tenantId,
        string pemPath,
        IEnumerable<string>? scopes = null) {
#if NET5_0_OR_GREATER
        var certificate = X509Certificate2.CreateFromPemFile(pemPath);
        return AcquireGraphCertificateTokenInternal(clientId, tenantId, certificate, scopes);
#else
        return Task.FromException<GraphAuthorization>(new NotSupportedException("PEM certificates are not supported on this framework."));
#endif
    }

    private static async Task<GraphAuthorization> AcquireGraphCertificateTokenInternal(
        string clientId,
        string tenantId,
        X509Certificate2 certificate,
        IEnumerable<string>? scopes) {
        var app = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithTenantId(tenantId)
            .WithCertificate(certificate)
            .Build();
        scopes ??= new[] { "https://graph.microsoft.com/.default" };
        var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
        return new GraphAuthorization {
            AccessToken = result.AccessToken,
            TokenType = "Bearer",
            ExpiresOn = result.ExpiresOn
        };
    }
}
