using Microsoft.Identity.Client;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;

namespace Mailozaurr;

public static class OAuthHelpers {
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
}