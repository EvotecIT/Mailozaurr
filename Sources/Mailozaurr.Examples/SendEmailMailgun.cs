using Mailozaurr;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Example sending an email using the <see cref="MailgunClient"/>.
/// </summary>
public static class SendEmailMailgun {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        // === CONFIGURATION ===
        string apiKey = "your-mailgun-api-key";
        string sender = "sender@yourdomain.com";
        string recipient = "recipient@example.com";

        // === EXAMPLE ===
        try {
            using var mailgun = new MailgunClient();
            mailgun.From = sender;
            mailgun.To = new[] { recipient }.ToList<object>();
            mailgun.Subject = "Test Email via Mailgun";
            mailgun.Text = "Hello from Mailozaurr via Mailgun!";
            mailgun.Html = "<p>Hello from Mailozaurr via Mailgun!</p>";
            mailgun.Credentials = new NetworkCredential("api", apiKey);
            using var cts = new CancellationTokenSource();
            var result = await mailgun.SendEmailAsync(cts.Token);
            Console.WriteLine(result.Status ? "Mailgun: Email sent!" : $"Mailgun: Failed: {result.Error}");
        } catch (Exception ex) {
            Console.WriteLine($"Mailgun Example Error: {ex.Message}");
        }
    }
}

