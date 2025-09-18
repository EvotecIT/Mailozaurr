using MimeKit;
using System.IO;
using System.Threading.Tasks;
using Mailozaurr;

/// <summary>
/// Example demonstrating how to persist and retrieve pending messages.
/// </summary>
public static class PendingMessageRepositoryExample {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        var dir = Path.Combine(Path.GetTempPath(), "pending");
        var options = new PendingMessageRepositoryOptions { DirectoryPath = dir };
        var repository = new FilePendingMessageRepository(options);

        // Pending message providers such as SendGrid, Mailgun, Gmail API, and SES automatically
        // upgrade legacy Base64 secrets to the new CredentialProtection format the next time a
        // record is saved, so existing queue directories can be reused without manual migration.

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.To.Add(MailboxAddress.Parse("recipient@example.com"));
        message.Subject = "Pending";
        message.Body = new TextPart("plain") { Text = "Hello" };
        using var ms = new MemoryStream();
        await message.WriteToAsync(ms);
        var record = new PendingMessageRecord {
            MessageId = message.MessageId ?? MimeKit.Utils.MimeUtils.GenerateMessageId(),
            MimeMessage = Convert.ToBase64String(ms.ToArray()),
            Timestamp = DateTimeOffset.UtcNow,
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        await repository.SaveAsync(record);

        await foreach (var r in repository.GetAllAsync()) {
            Console.WriteLine($"Pending: {r.MessageId}");
        }
    }
}
