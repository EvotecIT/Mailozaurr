using Mailozaurr.NonDeliveryReports;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Mailozaurr.Tests;

public class SentMessageRepositoryTests {
    [Fact]
    public async Task ResolverMatchesSavedRecord() {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
        try {
            var repo = new FileSentMessageRepository(path);
            var record = new SentMessageRecord {
                MessageId = "id1",
                Recipients = "c@d.com",
                Subject = "subject",
                Timestamp = DateTimeOffset.UtcNow
            };
            await repo.SaveAsync(record);
            var resolver = new SendLogResolver(repo);
            var ndr = new NonDeliveryReport { OriginalMessageId = "id1", FinalRecipient = "c@d.com" };
            var resolved = await resolver.ResolveAsync(ndr);
            Assert.NotNull(resolved);
            Assert.Equal("subject", resolved!.Subject);
        } finally {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsync_AppendsMultipleRecords() {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
        try {
            var repo = new FileSentMessageRepository(path);
            await repo.SaveAsync(new SentMessageRecord { MessageId = "1", Recipients = "a@b.com", Subject = "s1", Timestamp = DateTimeOffset.UtcNow });
            await repo.SaveAsync(new SentMessageRecord { MessageId = "2", Recipients = "c@d.com", Subject = "s2", Timestamp = DateTimeOffset.UtcNow });
            var record = await repo.GetByMessageIdAsync("2");
            Assert.NotNull(record);
            Assert.Equal("s2", record!.Subject);
        } finally {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task GetByMessageIdAsync_FastLookupInLargeLog() {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
        try {
            const int count = 10000;
            var newline = Encoding.UTF8.GetBytes(Environment.NewLine);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)) {
                for (int i = 0; i < count; i++) {
                    var record = new SentMessageRecord {
                        MessageId = i.ToString(),
                        Recipients = "a@b.com",
                        Subject = "s",
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    await JsonSerializer.SerializeAsync(stream, record);
                    await stream.WriteAsync(newline, 0, newline.Length);
                }
            }
            // Reinitialize repository to rebuild the index
            var repo = new FileSentMessageRepository(path);
            var targetId = (count - 1).ToString();
            var seqWatch = Stopwatch.StartNew();
            _ = await SequentialSearchAsync(path, targetId);
            seqWatch.Stop();
            var idxWatch = Stopwatch.StartNew();
            var recordFound = await repo.GetByMessageIdAsync(targetId);
            idxWatch.Stop();
            Assert.NotNull(recordFound);
            Assert.True(idxWatch.Elapsed < seqWatch.Elapsed);
        } finally {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Constructor_IgnoresMalformedLinesAndIndexesValidRecords() {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
        try {
            var newline = Encoding.UTF8.GetBytes(Environment.NewLine);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)) {
                await JsonSerializer.SerializeAsync(stream, new SentMessageRecord {
                    MessageId = "1",
                    Recipients = "a@b.com",
                    Subject = "ok-1",
                    Timestamp = DateTimeOffset.UtcNow
                });
                await stream.WriteAsync(newline, 0, newline.Length);
                await stream.WriteAsync(Encoding.UTF8.GetBytes("{not-json"), 0, Encoding.UTF8.GetByteCount("{not-json"));
                await stream.WriteAsync(newline, 0, newline.Length);
                await JsonSerializer.SerializeAsync(stream, new SentMessageRecord {
                    MessageId = "2",
                    Recipients = "c@d.com",
                    Subject = "ok-2",
                    Timestamp = DateTimeOffset.UtcNow
                });
                await stream.WriteAsync(newline, 0, newline.Length);
            }

            var repo = new FileSentMessageRepository(path);
            var record = await repo.GetByMessageIdAsync("2");

            Assert.NotNull(record);
            Assert.Equal("ok-2", record!.Subject);
        } finally {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task GetByMessageIdAsync_ReturnsNullWhenIndexedLineBecomesMalformed() {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
        try {
            var repo = new FileSentMessageRepository(path);
            await repo.SaveAsync(new SentMessageRecord {
                MessageId = "1",
                Recipients = "a@b.com",
                Subject = "subject",
                Timestamp = DateTimeOffset.UtcNow
            });

            File.WriteAllText(path, "{broken");

            var record = await repo.GetByMessageIdAsync("1");

            Assert.Null(record);
        } finally {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Constructor_RebuildsLfIndexedLogWithoutOffsetDrift() {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
        try {
            var payload = string.Join("\n", new[] {
                JsonSerializer.Serialize(new SentMessageRecord {
                    MessageId = "1",
                    Recipients = "first@example.com",
                    Subject = "first",
                    Timestamp = DateTimeOffset.UtcNow
                }, MailozaurrJsonContext.Default.SentMessageRecord),
                JsonSerializer.Serialize(new SentMessageRecord {
                    MessageId = "2",
                    Recipients = "second@example.com",
                    Subject = "second",
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(1)
                }, MailozaurrJsonContext.Default.SentMessageRecord)
            }) + "\n";
            File.WriteAllText(path, payload, Encoding.UTF8);

            var repo = new FileSentMessageRepository(path);
            var record = await repo.GetByMessageIdAsync("2");

            Assert.NotNull(record);
            Assert.Equal("second", record!.Subject);
            Assert.Equal("second@example.com", record.Recipients);
        } finally {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static async Task<SentMessageRecord?> SequentialSearchAsync(string path, string messageId) {
        using var read = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(read);
        while (!reader.EndOfStream) {
            string? line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) {
                continue;
            }
            var record = JsonSerializer.Deserialize<SentMessageRecord>(line);
            if (record != null && string.Equals(record.MessageId, messageId, StringComparison.OrdinalIgnoreCase)) {
                return record;
            }
        }
        return null;
    }
}