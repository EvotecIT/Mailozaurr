using Mailozaurr;
using System.IO;

/// <summary>
/// Example showing how failed sends are persisted for later retry.
/// </summary>
public static class SmtpPendingMessageExample {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        var path = Path.Combine(Path.GetTempPath(), "pending.log");
        var repository = new FilePendingMessageRepository(path);

        var smtp = new Smtp { PendingMessageRepository = repository };
        smtp.From = "sender@example.com";
        smtp.To = new[] { "recipient@example.com" };
        smtp.Subject = "Pending";
        smtp.TextBody = "Hello";
        smtp.CreateMessage();

        // Without a configured server this send will fail and the message will be queued
        var result = await smtp.SendAsync();
        Console.WriteLine($"Queued message id: {result.MessageId}");
    }
}

