using MailKit.Net.Pop3;
using MailKit.Security;
using MimeKit;
using System;
using System.Threading.Tasks;

public static class FetchPopMessages {
    public static async Task RunAsync() {
        // === CONFIGURATION ===
        string server = "pop.example.com";
        string username = "user@example.com";
        string password = "Pa55w0rd";

        // === EXAMPLE ===
        using var client = new Pop3Client();
        await client.ConnectAsync(server, 995, SecureSocketOptions.SslOnConnect);
        await client.AuthenticateAsync(username, password);
        for (int i = 0; i < client.Count; i++) {
            MimeMessage msg = await client.GetMessageAsync(i);
            if (msg.Date.UtcDateTime.Date != DateTime.UtcNow.Date)
                continue;
            Console.WriteLine($"{msg.Date.LocalDateTime}: {msg.Subject}");
        }
        await client.DisconnectAsync(true);
    }
}
