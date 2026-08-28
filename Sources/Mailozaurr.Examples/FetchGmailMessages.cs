using Mailozaurr;
using System;
using System.Threading.Tasks;

/// <summary>
/// Example demonstrating how to list messages using the Gmail API.
/// </summary>
public static class FetchGmailMessages {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        // === CONFIGURATION ===
        string gmailAccount = "user@gmail.com";
        string accessToken = "ya29.your_token"; // OAuth access token
        int maxResults = 5; // limit number of messages

        // === EXAMPLE ===
        try {
            var cred = new OAuthCredential {
                UserName = gmailAccount,
                AccessToken = accessToken,
                ExpiresOn = DateTimeOffset.MaxValue
            };
            using var client = new GmailApiClient(cred);
            var messages = await client.ListAsync("me", maxResults: maxResults);
            Console.WriteLine($"Gmail API: fetched {messages.Count} messages");
        } catch (GmailAuthenticationException ex) {
            Console.WriteLine($"Gmail API Authentication Error: {ex.Message}");
        } catch (Exception ex) {
            Console.WriteLine($"Gmail API Example Error: {ex.Message}");
        }
    }
}
