using Mailozaurr;
using MimeKit;
using System;
using System.Threading.Tasks;

/// <summary>
/// Sends a simple message using the Gmail API.
/// </summary>
public static class SendEmailGmailApi {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        // === CONFIGURATION ===
        string gmailAccount = "user@gmail.com";
        string accessToken = "ya29.your_token"; // OAuth access token
        string recipient = "recipient@example.com";

        // === EXAMPLE ===
        try {
            var cred = new OAuthCredential {
                UserName = gmailAccount,
                AccessToken = accessToken,
                ExpiresOn = DateTimeOffset.MaxValue
            };
            var client = new GmailApiClient(cred);
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(gmailAccount));
            message.To.Add(MailboxAddress.Parse(recipient));
            message.Subject = "Gmail API Test";
            message.Body = new TextPart("plain") { Text = "Hello from Mailozaurr via Gmail API!" };
            var sent = await client.SendAsync("me", message);
            Console.WriteLine($"Gmail API: sent id {sent.Id}");
        } catch (Exception ex) {
            Console.WriteLine($"Gmail API Example Error: {ex.Message}");
        }
    }
}
