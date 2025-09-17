using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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

    [Fact]
    public async Task ProcessAsync_RetriesRespectDelaysAndRetryLimit() {
        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var options = new PendingMessageRepositoryOptions {
            DirectoryPath = directory,
            FileNamingScheme = () => "pending.log"
        };
        Directory.CreateDirectory(directory);
        var repository = new FilePendingMessageRepository(options);
        var filePath = Path.Combine(directory, "pending.log");

        try {
            var baseTime = DateTimeOffset.Parse("2024-06-01T10:00:00Z");
            var currentTime = baseTime;
            var retryDelay = TimeSpan.FromMinutes(10);
            var leaseDuration = TimeSpan.FromMinutes(1);
            const int maxAttempts = 3;

            var record = new PendingMessageRecord {
                MessageId = Guid.NewGuid().ToString("N"),
                Timestamp = baseTime,
                NextAttemptAt = baseTime,
                Provider = EmailProvider.None
            };
            await repository.SaveAsync(record);

            var sender = new FailingPendingMessageSender();
            var pair = new KeyValuePair<EmailProvider, IPendingMessageSender>(EmailProvider.None, sender);
            var factory = new PendingMessageSenderFactory(new[] { pair });
            var processor = new PendingMessageProcessor(
                repository,
                factory,
                retryDelaySelector: _ => retryDelay,
                clock: () => currentTime,
                maxRetryAttempts: maxAttempts,
                processingLeaseDuration: leaseDuration);

            await processor.ProcessAsync();

            Assert.Equal(1, sender.Attempts);
            var pending = await repository.GetByMessageIdAsync(record.MessageId);
            Assert.NotNull(pending);
            Assert.Equal(1, pending!.AttemptCount);
            Assert.Equal(baseTime + retryDelay, pending.NextAttemptAt);
            Assert.Equal(1, CountNonEmptyLines(filePath));

            currentTime = baseTime.AddMinutes(5);
            await processor.ProcessAsync();
            pending = await repository.GetByMessageIdAsync(record.MessageId);
            Assert.NotNull(pending);
            Assert.Equal(1, pending!.AttemptCount);
            Assert.Equal(baseTime + retryDelay, pending.NextAttemptAt);
            Assert.Equal(1, sender.Attempts);

            currentTime = baseTime + retryDelay + TimeSpan.FromMinutes(1);
            await processor.ProcessAsync();
            pending = await repository.GetByMessageIdAsync(record.MessageId);
            Assert.NotNull(pending);
            Assert.Equal(2, pending!.AttemptCount);
            var expectedNextAttempt = currentTime + retryDelay;
            Assert.Equal(expectedNextAttempt, pending.NextAttemptAt);
            Assert.Equal(2, sender.Attempts);
            Assert.Equal(1, CountNonEmptyLines(filePath));

            currentTime = expectedNextAttempt.AddMinutes(-2);
            await processor.ProcessAsync();
            pending = await repository.GetByMessageIdAsync(record.MessageId);
            Assert.NotNull(pending);
            Assert.Equal(2, pending!.AttemptCount);
            Assert.Equal(expectedNextAttempt, pending.NextAttemptAt);
            Assert.Equal(2, sender.Attempts);

            currentTime = expectedNextAttempt.AddMinutes(1);
            await processor.ProcessAsync();
            pending = await repository.GetByMessageIdAsync(record.MessageId);
            Assert.Null(pending);
            Assert.Equal(maxAttempts, sender.Attempts);
            Assert.Equal(0, CountNonEmptyLines(filePath));
        } finally {
            if (File.Exists(filePath)) {
                File.Delete(filePath);
            }
            if (Directory.Exists(directory)) {
                Directory.Delete(directory, true);
            }
        }
    }

    private static int CountNonEmptyLines(string path) {
        if (!File.Exists(path)) {
            return 0;
        }

        var count = 0;
        foreach (var line in File.ReadAllLines(path)) {
            if (!string.IsNullOrWhiteSpace(line)) {
                count++;
            }
        }

        return count;
    }

    private sealed class FailingPendingMessageSender : IPendingMessageSender {
        public int Attempts { get; private set; }

        public Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
            Attempts++;
            throw new HttpRequestException("Simulated failure");
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
