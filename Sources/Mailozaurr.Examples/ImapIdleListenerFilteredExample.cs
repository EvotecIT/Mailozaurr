using MailKit.Net.Imap;
using MailKit.Security;
using MailKit.Search;
using System;
using System.Threading.Tasks;

/// <summary>
/// Demonstrates filtering when using <see cref="Mailozaurr.ImapIdleListener"/>.
/// </summary>
public static class ImapIdleListenerFilteredExample {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        // === CONFIGURATION ===
        const string server = "imap.example.com";
        const int port = 993;
        const string username = "user@example.com";
        const string password = "Pa55w0rd";

        // === EXAMPLE ===
        using var client = new ImapClient();
        await client.ConnectAsync(server, port, SecureSocketOptions.SslOnConnect);
        await client.AuthenticateAsync(username, password);

        // Listen only for messages from a specific sender
        var query = SearchQuery.FromContains("alice@example.com");
        var listener = new Mailozaurr.ImapIdleListener(client, searchQuery: query);
        listener.MessageArrived += (s, msg) =>
            Console.WriteLine($"New message from Alice: {msg.Message.Subject}");

        await listener.StartAsync();

        Console.WriteLine("Press any key to stop...");
        Console.ReadKey();

        listener.Stop();
    }
}
