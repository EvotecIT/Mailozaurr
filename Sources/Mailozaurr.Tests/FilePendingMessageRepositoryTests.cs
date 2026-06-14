using MimeKit;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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

    [Fact]
    public async Task SaveAsync_ReplacesExistingMessageWithSameId() {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var options = new PendingMessageRepositoryOptions { DirectoryPath = dir, FileNamingScheme = () => "pending.log" };
        var filePath = Path.Combine(dir, "pending.log");
        Directory.CreateDirectory(dir);

        try {
            var repo = new FilePendingMessageRepository(options);
            var messageId = Guid.NewGuid().ToString("N");
            var initial = new PendingMessageRecord {
                MessageId = messageId,
                Timestamp = DateTimeOffset.UtcNow,
                NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                Provider = EmailProvider.SendGrid,
                AttemptCount = 1
            };

            await repo.SaveAsync(initial);

            var updated = new PendingMessageRecord {
                MessageId = messageId,
                Timestamp = initial.Timestamp.AddMinutes(1),
                NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(10),
                Provider = EmailProvider.Mailgun,
                AttemptCount = 3
            };

            await repo.SaveAsync(updated);

            var loaded = await repo.GetByMessageIdAsync(messageId);
            Assert.NotNull(loaded);
            Assert.Equal(updated.AttemptCount, loaded!.AttemptCount);
            Assert.Equal(updated.Provider, loaded.Provider);
            Assert.Equal(updated.NextAttemptAt, loaded.NextAttemptAt);

            var lines = File.ReadAllLines(filePath).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

            Assert.Equal(2, lines.Count);

            using (var json = JsonDocument.Parse(lines[1])) {
                Assert.True(TryGetProperty(json.RootElement, "EntryType", out var entryTypeElement));
                Assert.Equal("upsert", entryTypeElement.GetString(), StringComparer.OrdinalIgnoreCase);
            }

            var records = await ReadAllAsync(repo);
            Assert.Single(records);
            Assert.Equal(messageId, records[0].MessageId);
            Assert.Equal(updated.AttemptCount, records[0].AttemptCount);
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
    public async Task SaveAsync_PerformsCompactionWhenThresholdReached() {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var options = new PendingMessageRepositoryOptions { DirectoryPath = dir, FileNamingScheme = () => "pending.log" };
        var filePath = Path.Combine(dir, "pending.log");
        Directory.CreateDirectory(dir);

        try {
            var repo = new FilePendingMessageRepository(options);
            var thresholdField = typeof(FilePendingMessageRepository).GetField("DefaultCompactionThreshold", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(thresholdField);
            var threshold = (int)thresholdField!.GetValue(null)!;
            var totalOperations = threshold + 5;

            var record = new PendingMessageRecord {
                MessageId = Guid.NewGuid().ToString("N"),
                Timestamp = DateTimeOffset.UtcNow,
                Provider = EmailProvider.SendGrid
            };

            for (var i = 0; i < totalOperations; i++) {
                record.AttemptCount = i;
                record.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(i);
                await repo.SaveAsync(record);
            }

            var lines = File.ReadAllLines(filePath).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
            Assert.True(lines.Count < totalOperations, $"Expected compaction to reduce log size. Entries: {lines.Count}, operations: {totalOperations}");

            var loaded = await repo.GetByMessageIdAsync(record.MessageId);
            Assert.NotNull(loaded);
            Assert.Equal(record.AttemptCount, loaded!.AttemptCount);
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
    public async Task RemoveAsync_AppendsTombstoneEntry() {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var options = new PendingMessageRepositoryOptions { DirectoryPath = dir, FileNamingScheme = () => "pending.log" };
        var filePath = Path.Combine(dir, "pending.log");
        Directory.CreateDirectory(dir);

        try {
            var repo = new FilePendingMessageRepository(options);
            var record = new PendingMessageRecord {
                MessageId = Guid.NewGuid().ToString("N"),
                Timestamp = DateTimeOffset.UtcNow,
                Provider = EmailProvider.Mailgun
            };

            await repo.SaveAsync(record);

            await repo.RemoveAsync(record.MessageId);

            var lines = File.ReadAllLines(filePath).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
            Assert.Equal(2, lines.Count);

            var lastLine = lines[lines.Count - 1];

            using (var json = JsonDocument.Parse(lastLine)) {
                Assert.True(TryGetProperty(json.RootElement, "EntryType", out var entryTypeElement));
                Assert.Equal("tombstone", entryTypeElement.GetString(), StringComparer.OrdinalIgnoreCase);
                Assert.True(TryGetProperty(json.RootElement, "MessageId", out var messageIdElement));
                Assert.Equal(record.MessageId, messageIdElement.GetString());
            }

            var loaded = await repo.GetByMessageIdAsync(record.MessageId);
            Assert.Null(loaded);
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
    public async Task Constructor_RebuildsLfIndexedLogWithoutOffsetDrift() {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var filePath = Path.Combine(dir, "pending.log");
        Directory.CreateDirectory(dir);

        try {
            var first = new PendingMessageRecord {
                MessageId = "msg-1",
                Timestamp = DateTimeOffset.UtcNow,
                Provider = EmailProvider.SendGrid
            };
            var second = new PendingMessageRecord {
                MessageId = "msg-2",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(1),
                Provider = EmailProvider.Mailgun
            };

            var payload = string.Join("\n", new[] {
                JsonSerializer.Serialize(new PendingMessageLogEnvelope { EntryType = "upsert", MessageId = first.MessageId, Record = first }, MailozaurrJsonContext.Default.PendingMessageLogEnvelope),
                JsonSerializer.Serialize(new PendingMessageLogEnvelope { EntryType = "upsert", MessageId = second.MessageId, Record = second }, MailozaurrJsonContext.Default.PendingMessageLogEnvelope)
            }) + "\n";
            File.WriteAllText(filePath, payload, Encoding.UTF8);

            var repository = new FilePendingMessageRepository(filePath);
            var loaded = await repository.GetByMessageIdAsync(second.MessageId);

            Assert.NotNull(loaded);
            Assert.Equal(second.MessageId, loaded!.MessageId);
            Assert.Equal(second.Provider, loaded.Provider);
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
    public async Task GetByMessageIdAsync_ReturnsNullWhenFileIsDeletedAfterIndexing() {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var options = new PendingMessageRepositoryOptions { DirectoryPath = dir, FileNamingScheme = () => "pending.log" };
        var filePath = Path.Combine(dir, "pending.log");
        Directory.CreateDirectory(dir);

        try {
            var repository = new FilePendingMessageRepository(options);
            var record = new PendingMessageRecord {
                MessageId = Guid.NewGuid().ToString("N"),
                Timestamp = DateTimeOffset.UtcNow,
                Provider = EmailProvider.SendGrid
            };

            await repository.SaveAsync(record);
            File.Delete(filePath);

            var loaded = await repository.GetByMessageIdAsync(record.MessageId);

            Assert.Null(loaded);
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
    public async Task GetAllAsync_ReturnsEmptyWhenFileIsDeletedAfterIndexing() {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var options = new PendingMessageRepositoryOptions { DirectoryPath = dir, FileNamingScheme = () => "pending.log" };
        var filePath = Path.Combine(dir, "pending.log");
        Directory.CreateDirectory(dir);

        try {
            var repository = new FilePendingMessageRepository(options);
            var record = new PendingMessageRecord {
                MessageId = Guid.NewGuid().ToString("N"),
                Timestamp = DateTimeOffset.UtcNow,
                Provider = EmailProvider.SendGrid
            };

            await repository.SaveAsync(record);
            File.Delete(filePath);

            var records = await ReadAllAsync(repository);

            Assert.Empty(records);
        } finally {
            if (File.Exists(filePath)) {
                File.Delete(filePath);
            }
            if (Directory.Exists(dir)) {
                Directory.Delete(dir, true);
            }
        }
    }

    private static async Task<List<PendingMessageRecord>> ReadAllAsync(IPendingMessageRepository repository) {
        var records = new List<PendingMessageRecord>();
        await foreach (var record in repository.GetAllAsync()) {
            records.Add(record);
        }

        return records;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value) {
        foreach (var property in element.EnumerateObject()) {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)) {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}