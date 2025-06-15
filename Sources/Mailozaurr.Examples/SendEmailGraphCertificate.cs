using Mailozaurr;
using System;
using System.Threading.Tasks;

public static class SendEmailGraphCertificate {
    public static async Task RunAsync() {
        // === CONFIGURATION ===
        string clientId = "your-client-id";
        string tenantId = "your-tenant-id";
        string certificatePath = "path-to-your.pfx";
        string certificatePassword = "your-cert-password";
        string sender = "sender@yourtenant.onmicrosoft.com";
        string recipient = "recipient@example.com";

        // === EXAMPLE ===
        try {
            var token = await OAuthHelpers.AcquireGraphCertificateTokenAsync(
                clientId,
                tenantId,
                certificatePath,
                certificatePassword);

            var graph = new Graph();
            graph.From = sender;
            graph.To = new[] { recipient };
            graph.Subject = "Test Email via Microsoft Graph (Certificate)";
            graph.HTML = "<p>Hello from Mailozaurr via Microsoft Graph (Certificate)!</p>";
            graph.AccessToken = token.AccessToken;
            graph.TokenType = token.TokenType;

            var sendResult = await graph.SendMessageAsync();
            Console.WriteLine(sendResult.Status
                ? "Graph (Certificate): Email sent!"
                : $"Graph (Certificate): Failed: {sendResult.Error}");
        } catch (Exception ex) {
            Console.WriteLine($"Graph (Certificate) Example Error: {ex.Message}");
        }
    }
}