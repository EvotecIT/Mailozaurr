using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.Tests;

public sealed class PendingMessageProcessorTests {
    private sealed class InMemoryPendingMessageRepository : IPendingMessageRepository {
        private readonly Dictionary<string, PendingMessageRecord> records = new(StringComparer.OrdinalIgnoreCase);

        public void Add(PendingMessageRecord record) {
            records[record.MessageId] = record;
        }

        public Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) {
            records[record.MessageId] = record;
            return Task.CompletedTask;
        }

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) {
            records.TryGetValue(messageId, out var record);
            PendingMessageRecord? result = record;
            return Task.FromResult<PendingMessageRecord?>(result);
        }

        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            foreach (var record in records.Values.ToList()) {
                cancellationToken.ThrowIfCancellationRequested();
                yield return record;
                await Task.Yield();
            }
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
            records.Remove(messageId);
            return Task.CompletedTask;
        }

        public bool Contains(string messageId) => records.ContainsKey(messageId);
    }

    private sealed class RecordingPendingMessageSender : IPendingMessageSender {
        public List<PendingMessageRecord> SentRecords { get; } = new();
        public bool ShouldThrow { get; set; }
        public Exception ExceptionToThrow { get; set; } = new InvalidOperationException("Send failure");

        public Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
            SentRecords.Add(record);
            if (ShouldThrow) {
                throw ExceptionToThrow;
            }

            return Task.CompletedTask;
        }
    }

    private static PendingMessageRecord CreateRecord(DateTimeOffset nextAttempt, string? messageId = null) => new() {
        MessageId = messageId ?? Guid.NewGuid().ToString("N"),
        Timestamp = nextAttempt,
        NextAttemptAt = nextAttempt,
        Provider = EmailProvider.None
    };

    [Fact]
    public async Task ProcessAsync_SendsDueMessagesAndRemovesThem() {
        var currentTime = DateTimeOffset.Parse("2024-06-01T12:00:00Z");
        var repository = new InMemoryPendingMessageRepository();
        var record = CreateRecord(currentTime.AddMinutes(-5));
        repository.Add(record);
        var sender = new RecordingPendingMessageSender();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(
            repository,
            factory,
            retryDelaySelector: _ => TimeSpan.FromMinutes(1),
            clock: () => currentTime);

        await processor.ProcessAsync();

        Assert.False(repository.Contains(record.MessageId));
        Assert.Single(sender.SentRecords);
        Assert.Equal(1, sender.SentRecords[0].AttemptCount);
    }

    [Fact]
    public async Task ProcessAsync_OnFailureSchedulesNextAttemptAndPersistsRecord() {
        var currentTime = DateTimeOffset.Parse("2024-06-02T08:30:00Z");
        var repository = new InMemoryPendingMessageRepository();
        var record = CreateRecord(currentTime);
        repository.Add(record);
        var sender = new RecordingPendingMessageSender {
            ShouldThrow = true,
            ExceptionToThrow = new Exception("Send failure")
        };
        var delay = TimeSpan.FromMinutes(15);
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(
            repository,
            factory,
            retryDelaySelector: _ => delay,
            clock: () => currentTime);

        await processor.ProcessAsync();

        var stored = await repository.GetByMessageIdAsync(record.MessageId);
        Assert.NotNull(stored);
        Assert.True(repository.Contains(record.MessageId));
        Assert.Equal(1, stored!.AttemptCount);
        Assert.Equal(currentTime + delay, stored.NextAttemptAt);
    }

    [Fact]
    public async Task ProcessAsync_SkipsRecordsNotYetDue() {
        var currentTime = DateTimeOffset.Parse("2024-06-03T09:00:00Z");
        var repository = new InMemoryPendingMessageRepository();
        var record = CreateRecord(currentTime.AddMinutes(10));
        repository.Add(record);
        var sender = new RecordingPendingMessageSender();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(
            repository,
            factory,
            retryDelaySelector: _ => TimeSpan.FromMinutes(5),
            clock: () => currentTime);

        await processor.ProcessAsync();

        var stored = await repository.GetByMessageIdAsync(record.MessageId);
        Assert.NotNull(stored);
        Assert.True(repository.Contains(record.MessageId));
        Assert.Equal(0, stored!.AttemptCount);
        Assert.Empty(sender.SentRecords);
        Assert.Equal(record.NextAttemptAt, stored.NextAttemptAt);
    }

    [Fact]
    public async Task ProcessAsync_SkipsRecordsWithMissingMessageId() {
        var currentTime = DateTimeOffset.Parse("2024-06-04T10:00:00Z");
        var repository = new InMemoryPendingMessageRepository();
        var invalid = CreateRecord(currentTime.AddMinutes(-2), string.Empty);
        var valid = CreateRecord(currentTime.AddMinutes(-2));
        repository.Add(invalid);
        repository.Add(valid);
        var sender = new RecordingPendingMessageSender();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(repository, factory, clock: () => currentTime);

        await processor.ProcessAsync();

        Assert.True(repository.Contains(string.Empty));
        Assert.False(repository.Contains(valid.MessageId));
        Assert.Single(sender.SentRecords);
        Assert.Equal(valid.MessageId, sender.SentRecords[0].MessageId);
    }

    [Fact]
    public async Task ProcessAsync_RemovesRecordWhenPermanentFailureOccurs() {
        var currentTime = DateTimeOffset.Parse("2024-06-05T09:30:00Z");
        var repository = new InMemoryPendingMessageRepository();
        var record = CreateRecord(currentTime.AddMinutes(-1));
        repository.Add(record);
        var sender = new RecordingPendingMessageSender {
            ShouldThrow = true,
            ExceptionToThrow = new InvalidOperationException("Permanent failure")
        };
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(repository, factory, clock: () => currentTime);

        await processor.ProcessAsync();

        Assert.False(repository.Contains(record.MessageId));
        Assert.Single(sender.SentRecords);
    }

    [Fact]
    public async Task ProcessAsync_StopsAfterCancellation() {
        var currentTime = DateTimeOffset.Parse("2024-06-06T07:15:00Z");
        var repository = new InMemoryPendingMessageRepository();
        var first = CreateRecord(currentTime.AddMinutes(-1));
        var second = CreateRecord(currentTime.AddMinutes(-1));
        repository.Add(first);
        repository.Add(second);
        var sender = new RecordingPendingMessageSender();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(repository, factory, clock: () => currentTime);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => processor.ProcessAsync(cts.Token));

        Assert.Empty(sender.SentRecords);
        Assert.True(repository.Contains(first.MessageId));
        Assert.True(repository.Contains(second.MessageId));
        Assert.Equal(0, first.AttemptCount);
        Assert.Equal(0, second.AttemptCount);
    }

    [Fact]
    public async Task ProcessAsync_RemovesRecordsThatExceededRetryLimit() {
        var currentTime = DateTimeOffset.Parse("2024-06-07T11:00:00Z");
        var repository = new InMemoryPendingMessageRepository();
        var record = CreateRecord(currentTime.AddMinutes(-1));
        record.AttemptCount = 5;
        repository.Add(record);
        var sender = new RecordingPendingMessageSender();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(repository, factory, clock: () => currentTime, maxRetryAttempts: 5);

        await processor.ProcessAsync();

        Assert.False(repository.Contains(record.MessageId));
        Assert.Empty(sender.SentRecords);
        Assert.Equal(5, record.AttemptCount);
    }

    [Fact]
    public async Task ProcessAsync_RemovesRecordAfterFinalFailedAttempt() {
        var currentTime = DateTimeOffset.Parse("2024-06-08T14:45:00Z");
        var repository = new InMemoryPendingMessageRepository();
        var record = CreateRecord(currentTime.AddMinutes(-1));
        record.AttemptCount = 4;
        repository.Add(record);
        var sender = new RecordingPendingMessageSender {
            ShouldThrow = true,
            ExceptionToThrow = new Exception("Transient failure")
        };
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(
            repository,
            factory,
            retryDelaySelector: _ => TimeSpan.FromMinutes(5),
            clock: () => currentTime,
            maxRetryAttempts: 5);

        await processor.ProcessAsync();

        Assert.False(repository.Contains(record.MessageId));
        Assert.Single(sender.SentRecords);
        Assert.Equal(5, record.AttemptCount);
    }
}
