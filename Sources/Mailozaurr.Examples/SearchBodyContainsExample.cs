using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Security;
using System;
using System.Threading.Tasks;
using Mailozaurr;

/// <summary>
/// Example demonstrating how to search message bodies.
/// </summary>
public static class SearchBodyContainsExample {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        string user = "user@example.com";
        string pass = "Pa55w0rd";

        using var imap = new ImapClient();
        await imap.ConnectAsync("imap.example.com", 993, SecureSocketOptions.SslOnConnect);
        await imap.AuthenticateAsync(user, pass);
        var imapMessages = await MailboxSearcher.SearchImapAsync(imap, bodyContains: "invoice");
        foreach (var m in imapMessages) Console.WriteLine($"IMAP: {m.Message.Subject}");
        await imap.DisconnectAsync(true);

        using var pop = new Pop3Client();
        await pop.ConnectAsync("pop.example.com", 995, SecureSocketOptions.SslOnConnect);
        await pop.AuthenticateAsync(user, pass);
        var popMessages = await MailboxSearcher.SearchPop3Async(pop, bodyContains: "invoice");
        foreach (var m in popMessages) Console.WriteLine($"POP3: {m.Message.Subject}");
        await pop.DisconnectAsync(true);
    }
}
