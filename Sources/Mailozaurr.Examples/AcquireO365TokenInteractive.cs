using Mailozaurr;
using System;
using System.Threading.Tasks;

/// <summary>
/// Obtains an OAuth token for Office 365 using the interactive flow.
/// </summary>
public static class AcquireO365TokenInteractive {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        // === CONFIGURATION ===
        string? login = "user@example.com";
        string clientId = "your-client-id";
        string tenantId = "your-tenant-id";
        string redirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient";
        string[] scopes = {
            "email",
            "offline_access",
            "https://outlook.office.com/IMAP.AccessAsUser.All",
            "https://outlook.office.com/POP.AccessAsUser.All",
            "https://outlook.office.com/SMTP.Send"
        };

        // === EXAMPLE ===
        try {
            var cred = await OAuthHelpers.AcquireO365TokenInteractiveAsync(
                login,
                clientId,
                tenantId,
                redirectUri,
                scopes);
            Console.WriteLine($"Token acquired for {cred.UserName}: {cred.AccessToken.Substring(0, 5)}...");
        } catch (Exception ex) {
            Console.WriteLine($"AcquireO365TokenInteractive Example Error: {ex.Message}");
        }
    }
}