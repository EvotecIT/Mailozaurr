using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.Tests;

public sealed class PendingMessageProcessorFileRepositoryTests {
    [Fact]
    public async Task ProcessAsync_CompletesAgainstFileRepository() {
        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var options = new PendingMessageRepositoryOptions {
            DirectoryPath = directory,
            FileNamingScheme = () => "pending.log"
        };
        Directory.CreateDirectory(directory);
        var repository = new FilePendingMessageRepository(options);

        try {
            var record = new PendingMessageRecord {
                MessageId = Guid.NewGuid().ToString("N"),
                Timestamp = DateTimeOffset.UtcNow,
                NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                Provider = EmailProvider.None,
                MimeMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes("payload"))
            };
            await repository.SaveAsync(record);

            var sender = new RecordingPendingMessageSender();
            var pair = new KeyValuePair<EmailProvider, IPendingMessageSender>(EmailProvider.None, sender);
            var factory = new PendingMessageSenderFactory(new[] { pair });
            var processor = new PendingMessageProcessor(repository, factory, clock: () => DateTimeOffset.UtcNow);

            await processor.ProcessAsync();

            Assert.Single(sender.SentRecords);
            Assert.Equal(record.MessageId, sender.SentRecords[0].MessageId);

            var remaining = await repository.GetByMessageIdAsync(record.MessageId);
            Assert.Null(remaining);
        } finally {
            if (File.Exists(Path.Combine(directory, "pending.log"))) {
                File.Delete(Path.Combine(directory, "pending.log"));
            }
            if (Directory.Exists(directory)) {
                Directory.Delete(directory, true);
            }
        }
    }

    private sealed class RecordingPendingMessageSender : IPendingMessageSender {
        public List<PendingMessageRecord> SentRecords { get; } = new();

        public Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
            SentRecords.Add(record);
            return Task.CompletedTask;
        }
    }
}
