using MailKit.Net.Imap;
using MailKit.Security;
using Mailozaurr;
using Mailozaurr.NonDeliveryReports;
using System;
using System.Threading.Tasks;

/// <summary>
/// Demonstrates searching for Non-Delivery Reports in an IMAP mailbox.
/// </summary>
public static class SearchNonDeliveryReportsExample {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        // === CONFIGURATION ===
        string server = "imap.example.com";
        string username = "user@example.com";
        string password = "Pa55w0rd";

        // === EXAMPLE ===
        using var client = new ImapClient();
        await client.ConnectAsync(server, 993, SecureSocketOptions.SslOnConnect);
        await client.AuthenticateAsync(username, password);
        var reports = await MailboxSearcher.SearchNonDeliveryReportsAsync(
            client,
            folder: null,
            since: DateTime.UtcNow.AddDays(-7),
            recipientContains: "user@example.com");
        foreach (NonDeliveryReport report in reports) {
            Console.WriteLine($"NDR for {report.FinalRecipient}: {report.Type}");
        }
        await client.DisconnectAsync(true);
    }
}
