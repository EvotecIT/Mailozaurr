using MailKit.Net.Imap;
using MailKit.Security;
using Mailozaurr;
using Mailozaurr.NonDeliveryReports;
using System;
using System.Threading.Tasks;

/// <summary>
/// Demonstrates using the non-delivery report service with IMAP.
/// </summary>
public static class NonDeliveryReportServiceExample {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        string server = "imap.example.com";
        string username = "user@example.com";
        string password = "Pa55w0rd";

        using var client = new ImapClient();
        await client.ConnectAsync(server, 993, SecureSocketOptions.SslOnConnect);
        await client.AuthenticateAsync(username, password);

        var repository = new FileSentMessageRepository("sent.json");
        var resolver = new SendLogResolver(repository);
        var service = new ImapNonDeliveryReportService(client, resolver);
        var results = await service.SearchAsync(DateTime.UtcNow.AddDays(-7));
        foreach (NonDeliveryReportResult result in results) {
            Console.WriteLine($"NDR for {result.Report.FinalRecipient}: {result.Report.Type}");
        }
        await client.DisconnectAsync(true);
    }
}