using Mailozaurr;
using System;
using System.Threading.Tasks;

/// <summary>
/// Obtains an OAuth token for Gmail using the interactive flow.
/// </summary>
public static class AcquireGoogleTokenInteractive {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        // === CONFIGURATION ===
        string gmailAccount = "user@gmail.com";
        string clientId = "your-client-id";
        string clientSecret = "your-client-secret";
        string[] scopes = { "https://mail.google.com/" };

        // === EXAMPLE ===
        try {
            var cred = await OAuthHelpers.AcquireGoogleTokenInteractiveAsync(
                gmailAccount,
                clientId,
                clientSecret,
                scopes);
            Console.WriteLine($"Token acquired for {cred.UserName}: {cred.AccessToken.Substring(0, 5)}...");
        } catch (Exception ex) {
            Console.WriteLine($"AcquireGoogleTokenInteractive Example Error: {ex.Message}");
        }
    }
}