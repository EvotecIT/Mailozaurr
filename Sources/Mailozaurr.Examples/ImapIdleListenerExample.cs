using MailKit.Net.Imap;
using MailKit.Security;
using System;
using System.Threading.Tasks;

public static class ImapIdleListenerExample {
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

        var listener = new Mailozaurr.ImapIdleListener(client);
        listener.MessageArrived += (s, msg) =>
            Console.WriteLine($"New message: {msg.Message.Subject}");

        await listener.StartAsync();

        Console.WriteLine("Press any key to stop...");
        Console.ReadKey();

        listener.Stop();
    }
}
