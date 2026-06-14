using MailKit.Net.Imap;
using Mailozaurr;
using Mailozaurr.DmarcReports;
using System;
using System.Threading.Tasks;

namespace Mailozaurr.Examples;

public static class RetrieveDmarcReportsExample {
    public static async Task RunAsync(ImapClient client) {
        var reports = await MailboxSearcher.SearchDmarcReportsAsync(client, "INBOX", since: DateTime.UtcNow.AddDays(-7), domain: "example.com");
        foreach (var report in reports) {
            foreach (var att in report.Attachments) {
                using (att) {
                    DomainDetective.Process(att.Content, att.Name);
                }
            }
        }
    }
}

public static class DomainDetective {
    public static void Process(Stream zip, string name) {
        // Placeholder for integration with Domain Detective analysis
        Console.WriteLine($"Processing {name}");
    }
}