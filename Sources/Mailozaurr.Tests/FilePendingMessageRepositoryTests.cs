using System.IO;
using System.Threading.Tasks;
using MimeKit;

namespace Mailozaurr.Tests;

public sealed class FilePendingMessageRepositoryTests {
    [Fact]
    public async Task SaveRetrieveAndRemove() {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try {
            var repo = new FilePendingMessageRepository(path);
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
                Timestamp = DateTimeOffset.UtcNow
            };
            await repo.SaveAsync(record);

            var loaded = await repo.GetByMessageIdAsync(record.MessageId);
            Assert.NotNull(loaded);
            Assert.True(loaded!.NextAttemptAt <= DateTimeOffset.UtcNow);
            using var ms2 = new MemoryStream(Convert.FromBase64String(loaded!.MimeMessage));
            var restored = await MimeMessage.LoadAsync(ms2);
            Assert.Equal("Pending", restored.Subject);

            await repo.RemoveAsync(record.MessageId);
            var removed = await repo.GetByMessageIdAsync(record.MessageId);
            Assert.Null(removed);
        } finally {
            if (File.Exists(path)) {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task UsesOptions() {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var opts = new PendingMessageRepositoryOptions {
            DirectoryPath = dir,
            FileNameFactory = _ => "custom.log"
        };
        var repo = new FilePendingMessageRepository(opts);
        var record = new PendingMessageRecord {
            MessageId = "1",
            MimeMessage = string.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };
        await repo.SaveAsync(record);
        Assert.True(File.Exists(Path.Combine(dir, "custom.log")));
        File.Delete(Path.Combine(dir, "custom.log"));
    }
}
