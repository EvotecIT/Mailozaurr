using Mailozaurr;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Example sending a templated email using <see cref="SesClient"/>.
/// </summary>
public static class SendTemplatedEmailSes {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        // === CONFIGURATION ===
        string accessKey = "your-access-key";
        string secretKey = "your-secret-key";
        string sender = "sender@example.com";
        string recipient = "recipient@example.com";

        // === EXAMPLE ===
        try {
            using var ses = new SesClient();
            ses.Credentials = new NetworkCredential(accessKey, secretKey);
            ses.From = sender;
            ses.To = new List<object> { recipient };
            ses.TemplateName = "MyTemplate";
            ses.TemplateData = new Dictionary<string, string> { ["Name"] = "John" };
            using var cts = new CancellationTokenSource();
            var result = await ses.SendTemplatedEmailAsync(cts.Token);
            Console.WriteLine(result.Status ? "SES: Email sent!" : $"SES: Failed: {result.Error}");
        } catch (Exception ex) {
            Console.WriteLine($"SES Example Error: {ex.Message}");
        }
    }
}

