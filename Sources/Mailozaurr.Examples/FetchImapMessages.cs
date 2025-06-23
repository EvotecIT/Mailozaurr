using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using System;
using System.Threading.Tasks;

public static class FetchImapMessages {
    public static async Task RunAsync() {
        // === CONFIGURATION ===
        string server = "imap.example.com";
        string username = "user@example.com";
        string password = "Pa55w0rd";

        // === EXAMPLE ===
        using var client = new ImapClient();
        await client.ConnectAsync(server, 993, SecureSocketOptions.SslOnConnect);
        await client.AuthenticateAsync(username, password);
        var folder = client.GetFolder("Inbox/Reports");
        await folder.OpenAsync(FolderAccess.ReadOnly);
        var query = SearchQuery.SubjectContains("Monthly").And(SearchQuery.DeliveredAfter(DateTime.UtcNow.AddDays(-7)));
        var uids = await folder.SearchAsync(query);
        foreach (var uid in uids) {
            MimeMessage msg = await folder.GetMessageAsync(uid);
            Console.WriteLine($"{msg.Date.LocalDateTime}: {msg.Subject}");
        }
        await client.DisconnectAsync(true);
    }
}
