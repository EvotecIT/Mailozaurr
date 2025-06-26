using Mailozaurr;
using System;
using System.Net;
using System.Threading.Tasks;

public static class SendEmailGraphClientSecret {
    public static async Task RunAsync() {
        // === CONFIGURATION ===
        string clientId = "your-client-id";
        string tenantId = "your-tenant-id";
        string clientSecret = "your-client-secret";
        string sender = "sender@yourtenant.onmicrosoft.com";
        string recipient = "recipient@example.com";

        // === EXAMPLE ===
        try {
            using var graph = new Graph();
            graph.From = sender;
            graph.To = new[] { recipient };
            graph.Subject = "Test Email via Microsoft Graph (Client Secret)";
            graph.HTML = "<p>Hello from Mailozaurr via Microsoft Graph (Client Secret)!</p>";
            // Authenticate: username = clientid@tenantid, password = client secret
            var credential = new NetworkCredential($"{clientId}@{tenantId}", clientSecret);
            graph.Authenticate(credential);
            var connectResult = await graph.ConnectO365GraphAsync();
            if (!connectResult.Status)
                throw new Exception($"Graph Connect failed: {connectResult.Error}");
            var sendResult = await graph.SendMessageAsync();
            Console.WriteLine(sendResult.Status ? "Graph (Client Secret): Email sent!" : $"Graph (Client Secret): Failed: {sendResult.Error}");
        } catch (Exception ex) {
            Console.WriteLine($"Graph (Client Secret) Example Error: {ex.Message}");
        }
    }
}