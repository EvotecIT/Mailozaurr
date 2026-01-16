using MailKit.Net.Imap;
using MailKit.Security;
using Mailozaurr;
using Mailozaurr.NonDeliveryReports;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Demonstrates retrieving non-delivery reports and correlating them with sent messages.
/// </summary>
public static class RetrieveAndCorrelateNonDeliveryReportsExample {
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
        IList<NonDeliveryReport> reports = await MailboxSearcher.SearchNonDeliveryReportsAsync(
            client,
            since: DateTime.UtcNow.AddDays(-7));
        foreach (var report in reports) {
            var match = await resolver.ResolveAsync(report);
            if (match != null) {
                Console.WriteLine($"NDR for {report.FinalRecipient} matches message '{match.Subject}'");
            }
        }

        await client.DisconnectAsync(true);
    }
}
