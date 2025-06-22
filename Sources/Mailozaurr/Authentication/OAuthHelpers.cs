using Microsoft.Identity.Client;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;
using System.Security.Cryptography.X509Certificates;

namespace Mailozaurr;

/// <summary>
/// Helper methods for acquiring OAuth tokens for various services.
/// </summary>
public static class OAuthHelpers {
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
        string login,
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
        AuthenticationResult result;
        if (!string.IsNullOrEmpty(login)) {
            result = await app.AcquireTokenInteractive(scopes)
                .WithLoginHint(login)
                .ExecuteAsync();
        } else {
            result = await app.AcquireTokenInteractive(scopes)
                .ExecuteAsync();
        }
        return new OAuthCredential {
            UserName = result.Account.Username,
            AccessToken = result.AccessToken
        };
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
        if (credential.Token.IsExpired(Google.Apis.Util.SystemClock.Default)) {
            await credential.RefreshTokenAsync(System.Threading.CancellationToken.None);
        }
        return new OAuthCredential {
            UserName = credential.UserId,
            AccessToken = credential.Token.AccessToken
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
        var app = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithTenantId(tenantId)
            .WithCertificate(certificate)
            .Build();
        scopes ??= new[] { "https://graph.microsoft.com/.default" };
        var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
        return new GraphAuthorization {
            AccessToken = result.AccessToken,
            TokenType = "Bearer"
        };
    }
}