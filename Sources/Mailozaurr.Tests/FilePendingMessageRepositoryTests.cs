using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using MimeKit;

namespace Mailozaurr.Tests;

public sealed class FilePendingMessageRepositoryTests {
    [Fact]
    public async Task SaveRetrieveAndRemove() {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var options = new PendingMessageRepositoryOptions { DirectoryPath = dir, FileNamingScheme = () => "pending.log" };
        var filePath = Path.Combine(dir, "pending.log");
        try {
            var repo = new FilePendingMessageRepository(options);
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
                Provider = EmailProvider.SendGrid
            };
            record.ProviderData["ApiKeyId"] = "sendgrid-key";
            await repo.SaveAsync(record);

            var loaded = await repo.GetByMessageIdAsync(record.MessageId);
            Assert.NotNull(loaded);
            Assert.True(loaded!.NextAttemptAt <= DateTimeOffset.UtcNow);
            Assert.Equal(EmailProvider.SendGrid, loaded.Provider);
            Assert.Equal("sendgrid-key", loaded.ProviderData["ApiKeyId"]);
            using var ms2 = new MemoryStream(Convert.FromBase64String(loaded!.MimeMessage));
            var restored = await MimeMessage.LoadAsync(ms2);
            Assert.Equal("Pending", restored.Subject);

            await repo.RemoveAsync(record.MessageId);
            var removed = await repo.GetByMessageIdAsync(record.MessageId);
            Assert.Null(removed);
        } finally {
            if (File.Exists(filePath)) {
                File.Delete(filePath);
            }
            if (Directory.Exists(dir)) {
                Directory.Delete(dir, true);
            }
        }
    }

    [Fact]
    public void DefaultsToTempPath() {
        var repo = new FilePendingMessageRepository();
        var field = typeof(FilePendingMessageRepository).GetField("filePath", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var path = (string)field.GetValue(repo)!;
        Assert.StartsWith(Path.GetTempPath(), path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DirectoryPath_MustNotBeNullOrEmpty() {
        var options = new PendingMessageRepositoryOptions();
        Assert.Throws<ArgumentException>(() => options.DirectoryPath = null!);
        Assert.Throws<ArgumentException>(() => options.DirectoryPath = "");
    }

    [Fact]
    public void FileNamingScheme_ExceptionWrapped() {
        var options = new PendingMessageRepositoryOptions {
            FileNamingScheme = () => throw new InvalidOperationException("boom")
        };
        var ex = Assert.Throws<InvalidOperationException>(() => new FilePendingMessageRepository(options));
        Assert.Contains("FileNamingScheme", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }
}
