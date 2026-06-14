using Mailozaurr;
using System.IO;

/// <summary>
/// Example showing how to replay messages from a pending repository.
/// </summary>
public static class SmtpProcessPendingMessagesExample {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        var pendingPath = Path.Combine(Path.GetTempPath(), "pending");
        var sentPath = Path.Combine(Path.GetTempPath(), "sent.log");
        var sentRepo = new FileSentMessageRepository(sentPath);

        var smtp = new Smtp {
            PendingMessagesPath = pendingPath,
            SentMessageRepository = sentRepo
        };

        await smtp.ProcessPendingMessagesAsync();
    }
}