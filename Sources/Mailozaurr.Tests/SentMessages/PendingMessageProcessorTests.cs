using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.Tests;

public sealed class PendingMessageProcessorTests {
    private sealed class InMemoryPendingMessageRepository : IPendingMessageRepository {
        private readonly ConcurrentDictionary<string, PendingMessageRecord> records = new(StringComparer.OrdinalIgnoreCase);
        private readonly object syncRoot = new();

        public void Add(PendingMessageRecord record) {
            records[record.MessageId] = record;
        }

        public Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) {
            records[record.MessageId] = record;
            return Task.CompletedTask;
        }

        public Task<PendingMessageRecord?> TryAcquireLeaseAsync(
            string messageId,
            DateTimeOffset dueBeforeOrAt,
            DateTimeOffset leaseUntil,
            CancellationToken cancellationToken = default) {
            lock (syncRoot) {
                if (!records.TryGetValue(messageId, out var record) || record.NextAttemptAt > dueBeforeOrAt) {
                    return Task.FromResult<PendingMessageRecord?>(null);
                }

                record.NextAttemptAt = leaseUntil;
                return Task.FromResult<PendingMessageRecord?>(record);
            }
        }

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) {
            records.TryGetValue(messageId, out var record);
            return Task.FromResult<PendingMessageRecord?>(record);
        }

        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            foreach (var record in records.Values) {
                cancellationToken.ThrowIfCancellationRequested();
                yield return record;
                await Task.Yield();
            }
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
            records.TryRemove(messageId, out _);
            return Task.CompletedTask;
        }

        public bool Contains(string messageId) => records.ContainsKey(messageId);
    }

    private sealed class InMemoryDeadLetterRepository : IPendingMessageDeadLetterRepository {
        private readonly ConcurrentDictionary<string, PendingMessageDeadLetterRecord> records = new(StringComparer.OrdinalIgnoreCase);
        private int saveCount;

        public int SaveCount => saveCount;

        public Task SaveAsync(PendingMessageDeadLetterRecord record, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref saveCount);
            records[record.Message.MessageId] = record;
            return Task.CompletedTask;
        }

        public Task<PendingMessageDeadLetterRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) {
            records.TryGetValue(messageId, out var record);
            return Task.FromResult<PendingMessageDeadLetterRecord?>(record);
        }

        public async IAsyncEnumerable<PendingMessageDeadLetterRecord> GetAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            foreach (var record in records.Values) {
                cancellationToken.ThrowIfCancellationRequested();
                yield return record;
                await Task.Yield();
            }
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
            records.TryRemove(messageId, out _);
            return Task.CompletedTask;
        }
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

    private sealed class CancellingPendingMessageSender : IPendingMessageSender {
        private readonly CancellationTokenSource cts;

        public CancellingPendingMessageSender(CancellationTokenSource cts) {
            this.cts = cts;
        }

        public Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingObserver : IPendingMessageProcessorObserver {
        public List<(PendingMessageRecord Record, PendingMessageSkipReason Reason)> Skipped { get; } = new();
        public List<(PendingMessageRecord Record, int Attempt)> Started { get; } = new();
        public List<(PendingMessageRecord Record, int Attempt, TimeSpan Duration)> Sent { get; } = new();
        public List<MessageFailureEvent> Failed { get; } = new();
        public List<MessageDropEvent> Dropped { get; } = new();

        public void MessageSkipped(PendingMessageRecord record, PendingMessageSkipReason reason) {
            Skipped.Add((record, reason));
        }

        public void MessageAttemptStarted(PendingMessageRecord record, int attempt) {
            Started.Add((record, attempt));
        }

        public void MessageSent(PendingMessageRecord record, int attempt, TimeSpan duration) {
            Sent.Add((record, attempt, duration));
        }

        public void MessageFailed(
            PendingMessageRecord record,
            int attempt,
            Exception exception,
            TimeSpan duration,
            bool willRetry,
            TimeSpan? retryDelay) {
            Failed.Add(new MessageFailureEvent(record, attempt, exception, duration, willRetry, retryDelay));
        }

        public void MessageDropped(
            PendingMessageRecord record,
            int attempt,
            PendingMessageDropReason reason,
            Exception? exception) {
            Dropped.Add(new MessageDropEvent(record, attempt, reason, exception));
        }

        public sealed class MessageFailureEvent {
            public MessageFailureEvent(
                PendingMessageRecord record,
                int attempt,
                Exception exception,
                TimeSpan duration,
                bool willRetry,
                TimeSpan? retryDelay) {
                Record = record;
                Attempt = attempt;
                Exception = exception;
                Duration = duration;
                WillRetry = willRetry;
                RetryDelay = retryDelay;
            }

            public PendingMessageRecord Record { get; }
            public int Attempt { get; }
            public Exception Exception { get; }
            public TimeSpan Duration { get; }
            public bool WillRetry { get; }
            public TimeSpan? RetryDelay { get; }
        }

        public sealed class MessageDropEvent {
            public MessageDropEvent(
                PendingMessageRecord record,
                int attempt,
                PendingMessageDropReason reason,
                Exception? exception) {
                Record = record;
                Attempt = attempt;
                Reason = reason;
                Exception = exception;
            }

            public PendingMessageRecord Record { get; }
            public int Attempt { get; }
            public PendingMessageDropReason Reason { get; }
            public Exception? Exception { get; }
        }
    }

    private static readonly EmailProvider[] ProvidersUnderTest = new[] {
        EmailProvider.None,
        EmailProvider.SendGrid,
        EmailProvider.Mailgun,
        EmailProvider.SES,
        EmailProvider.Gmail,
        EmailProvider.Graph
    };

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
        var observer = new RecordingObserver();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var lease = TimeSpan.FromMinutes(2);
        var processor = new PendingMessageProcessor(
            repository,
            factory,
            retryDelaySelector: _ => TimeSpan.FromMinutes(1),
            clock: () => currentTime,
            observer: observer,
            processingLeaseDuration: lease);

        await processor.ProcessAsync();

        Assert.False(repository.Contains(record.MessageId));
        Assert.Single(sender.SentRecords);
        Assert.Equal(1, sender.SentRecords[0].AttemptCount);
        Assert.Equal(currentTime + lease, sender.SentRecords[0].NextAttemptAt);
        Assert.Single(observer.Started);
        Assert.Single(observer.Sent);
        Assert.Empty(observer.Failed);
        Assert.Equal(1, observer.Started[0].Attempt);
        Assert.True(observer.Sent[0].Duration >= TimeSpan.Zero);
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
        var observer = new RecordingObserver();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(
            repository,
            factory,
            retryDelaySelector: _ => delay,
            clock: () => currentTime,
            observer: observer);

        await processor.ProcessAsync();

        var stored = await repository.GetByMessageIdAsync(record.MessageId);
        Assert.NotNull(stored);
        Assert.True(repository.Contains(record.MessageId));
        Assert.Equal(1, stored!.AttemptCount);
        Assert.Equal(currentTime + delay, stored.NextAttemptAt);
        var failure = Assert.Single(observer.Failed);
        Assert.True(failure.WillRetry);
        Assert.True(failure.RetryDelay.HasValue);
        Assert.Equal(delay, failure.RetryDelay.Value);
    }

    [Fact]
    public async Task ProcessAsync_ReleasesLeaseWhenCancelled() {
        var currentTime = DateTimeOffset.Parse("2024-07-01T09:00:00Z");
        var repository = new InMemoryPendingMessageRepository();
        var record = CreateRecord(currentTime.AddMinutes(-1));
        repository.Add(record);
        using var cts = new CancellationTokenSource();
        var sender = new CancellingPendingMessageSender(cts);
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(repository, factory, clock: () => currentTime);

        await Assert.ThrowsAsync<OperationCanceledException>(() => processor.ProcessAsync(cts.Token));

        var stored = await repository.GetByMessageIdAsync(record.MessageId);
        Assert.NotNull(stored);
        Assert.Equal(0, stored!.AttemptCount);
        Assert.Equal(currentTime, stored.NextAttemptAt);
    }

    [Fact]
    public async Task ProcessAsync_SkipsRecordsNotYetDue() {
        var currentTime = DateTimeOffset.Parse("2024-06-03T09:00:00Z");
        var repository = new InMemoryPendingMessageRepository();
        var record = CreateRecord(currentTime.AddMinutes(10));
        repository.Add(record);
        var sender = new RecordingPendingMessageSender();
        var observer = new RecordingObserver();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(
            repository,
            factory,
            retryDelaySelector: _ => TimeSpan.FromMinutes(5),
            clock: () => currentTime,
            observer: observer);

        await processor.ProcessAsync();

        var stored = await repository.GetByMessageIdAsync(record.MessageId);
        Assert.NotNull(stored);
        Assert.True(repository.Contains(record.MessageId));
        Assert.Equal(0, stored!.AttemptCount);
        Assert.Empty(sender.SentRecords);
        Assert.Equal(record.NextAttemptAt, stored.NextAttemptAt);
        Assert.Single(observer.Skipped);
        Assert.Equal(PendingMessageSkipReason.NotDue, observer.Skipped[0].Reason);
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
        var observer = new RecordingObserver();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(repository, factory, clock: () => currentTime, observer: observer);

        await processor.ProcessAsync();

        Assert.True(repository.Contains(string.Empty));
        Assert.False(repository.Contains(valid.MessageId));
        Assert.Single(sender.SentRecords);
        Assert.Equal(valid.MessageId, sender.SentRecords[0].MessageId);
        Assert.Single(observer.Skipped);
        Assert.Equal(PendingMessageSkipReason.MissingMessageId, observer.Skipped[0].Reason);
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
        var observer = new RecordingObserver();
        var deadLetters = new InMemoryDeadLetterRepository();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(repository, factory, clock: () => currentTime, observer: observer, deadLetterRepository: deadLetters);

        await processor.ProcessAsync();

        Assert.False(repository.Contains(record.MessageId));
        var deadLetter = await deadLetters.GetByMessageIdAsync(record.MessageId);
        Assert.NotNull(deadLetter);
        Assert.Equal(PendingMessageDropReason.PermanentFailure, deadLetter!.Reason);
        Assert.Equal("Permanent failure", deadLetter.ErrorMessage);
        Assert.Single(sender.SentRecords);
        var failure = Assert.Single(observer.Failed);
        Assert.False(failure.WillRetry);
        Assert.Null(failure.RetryDelay);
        var drop = Assert.Single(observer.Dropped);
        Assert.Equal(PendingMessageDropReason.PermanentFailure, drop.Reason);
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
        var observer = new RecordingObserver();
        var deadLetters = new InMemoryDeadLetterRepository();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(repository, factory, clock: () => currentTime, maxRetryAttempts: 5, observer: observer, deadLetterRepository: deadLetters);

        await processor.ProcessAsync();

        Assert.False(repository.Contains(record.MessageId));
        var deadLetter = await deadLetters.GetByMessageIdAsync(record.MessageId);
        Assert.NotNull(deadLetter);
        Assert.Equal(PendingMessageDropReason.RetryLimitReached, deadLetter!.Reason);
        Assert.Empty(sender.SentRecords);
        Assert.Equal(5, record.AttemptCount);
        Assert.Single(observer.Dropped);
        Assert.Equal(PendingMessageDropReason.RetryLimitReached, observer.Dropped[0].Reason);
        Assert.Equal(5, observer.Dropped[0].Attempt);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotDoubleDeadLetterExhaustedRecordWhenTwoProcessorsRace() {
        var currentTime = DateTimeOffset.Parse("2024-06-07T11:30:00Z");
        var repository = new CoordinatedPendingMessageRepository(currentTime.AddMinutes(-1), attemptCount: 5);
        var deadLetters = new InMemoryDeadLetterRepository();
        var observer = new RecordingObserver();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, new RecordingPendingMessageSender() }
        });
        var first = new PendingMessageProcessor(repository, factory, clock: () => currentTime, maxRetryAttempts: 5, observer: observer, deadLetterRepository: deadLetters);
        var second = new PendingMessageProcessor(repository, factory, clock: () => currentTime, maxRetryAttempts: 5, observer: observer, deadLetterRepository: deadLetters);

        var firstTask = first.ProcessAsync();
        var secondTask = second.ProcessAsync();

        repository.ReleaseEnumerations();
        await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(1, deadLetters.SaveCount);
        Assert.Single(observer.Dropped);
        Assert.Contains(observer.Skipped, skipped => skipped.Reason == PendingMessageSkipReason.LeaseNotAcquired);
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
        var observer = new RecordingObserver();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(
            repository,
            factory,
            retryDelaySelector: _ => TimeSpan.FromMinutes(5),
            clock: () => currentTime,
            maxRetryAttempts: 5,
            observer: observer);

        await processor.ProcessAsync();

        Assert.False(repository.Contains(record.MessageId));
        Assert.Single(sender.SentRecords);
        Assert.Equal(5, record.AttemptCount);
        var failure = Assert.Single(observer.Failed);
        Assert.False(failure.WillRetry);
        Assert.Null(failure.RetryDelay);
        var drop = Assert.Single(observer.Dropped);
        Assert.Equal(PendingMessageDropReason.RetryLimitReached, drop.Reason);
    }

    [Fact]
    public async Task ProcessAsync_UsesCustomPermanentFailureDetector() {
        var currentTime = DateTimeOffset.Parse("2024-06-09T12:00:00Z");
        var repository = new InMemoryPendingMessageRepository();
        var record = CreateRecord(currentTime.AddMinutes(-1));
        repository.Add(record);
        var sender = new RecordingPendingMessageSender {
            ShouldThrow = true,
            ExceptionToThrow = new HttpRequestException("Unrecoverable remote error")
        };
        var observer = new RecordingObserver();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(
            repository,
            factory,
            clock: () => currentTime,
            observer: observer,
            permanentFailureDetector: ex => ex is HttpRequestException);

        await processor.ProcessAsync();

        Assert.False(repository.Contains(record.MessageId));
        var failure = Assert.Single(observer.Failed);
        Assert.False(failure.WillRetry);
        Assert.Null(failure.RetryDelay);
        var drop = Assert.Single(observer.Dropped);
        Assert.Equal(PendingMessageDropReason.PermanentFailure, drop.Reason);
        Assert.IsType<HttpRequestException>(drop.Exception);
    }

    [Fact]
    public async Task ProcessAsync_NormalizesNegativeRetryDelayToZero() {
        var currentTime = DateTimeOffset.Parse("2024-06-10T10:15:00Z");
        var repository = new InMemoryPendingMessageRepository();
        var record = CreateRecord(currentTime.AddMinutes(-1));
        repository.Add(record);
        var sender = new RecordingPendingMessageSender {
            ShouldThrow = true,
            ExceptionToThrow = new Exception("Transient")
        };
        var observer = new RecordingObserver();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(
            repository,
            factory,
            retryDelaySelector: _ => TimeSpan.FromMinutes(-5),
            clock: () => currentTime,
            observer: observer,
            maxRetryAttempts: 3);

        await processor.ProcessAsync();

        var stored = await repository.GetByMessageIdAsync(record.MessageId);
        Assert.NotNull(stored);
        Assert.Equal(currentTime, stored!.NextAttemptAt);
        var failure = Assert.Single(observer.Failed);
        Assert.True(failure.RetryDelay.HasValue);
        Assert.Equal(TimeSpan.Zero, failure.RetryDelay.Value);
    }

    public static IEnumerable<object[]> ProviderDispatchData() {
        foreach (var provider in ProvidersUnderTest) {
            yield return new object[] { provider };
        }
    }

    [Theory]
    [MemberData(nameof(ProviderDispatchData))]
    public async Task ProcessAsync_DispatchesToRegisteredProviderSender(EmailProvider provider) {
        var currentTime = DateTimeOffset.Parse("2024-06-11T08:00:00Z");
        var repository = new InMemoryPendingMessageRepository();
        var record = CreateRecord(currentTime.AddMinutes(-1));
        record.Provider = provider;
        repository.Add(record);
        var senders = new Dictionary<EmailProvider, RecordingPendingMessageSender>();
        var entries = new List<KeyValuePair<EmailProvider, IPendingMessageSender>>();
        foreach (var providerUnderTest in ProvidersUnderTest) {
            var stub = new RecordingPendingMessageSender();
            senders[providerUnderTest] = stub;
            entries.Add(new KeyValuePair<EmailProvider, IPendingMessageSender>(providerUnderTest, stub));
        }

        var factory = new PendingMessageSenderFactory(entries);
        var processor = new PendingMessageProcessor(
            repository,
            factory,
            clock: () => currentTime,
            processingLeaseDuration: TimeSpan.Zero);

        await processor.ProcessAsync();

        Assert.False(repository.Contains(record.MessageId));
        Assert.Single(senders[provider].SentRecords);
        Assert.Equal(provider, senders[provider].SentRecords[0].Provider);
        foreach (var pair in senders) {
            if (pair.Key == provider) {
                continue;
            }

            Assert.Empty(pair.Value.SentRecords);
        }
    }

    [Fact]
    public async Task ProcessAsync_UsesSafeMinimumLeaseWhenConfiguredLeaseIsZero() {
        var currentTime = DateTimeOffset.Parse("2024-06-11T08:00:00Z");
        var repository = new InMemoryPendingMessageRepository();
        var record = CreateRecord(currentTime.AddMinutes(-1));
        repository.Add(record);
        var sender = new RecordingPendingMessageSender();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var processor = new PendingMessageProcessor(
            repository,
            factory,
            clock: () => currentTime,
            processingLeaseDuration: TimeSpan.Zero);

        await processor.ProcessAsync();

        var sent = Assert.Single(sender.SentRecords);
        Assert.Equal(currentTime + TimeSpan.FromSeconds(30), sent.NextAttemptAt);
    }

    [Fact]
    public async Task ProcessAsync_RetriesUsingDelaySelectorPerAttempt() {
        var currentTime = DateTimeOffset.Parse("2024-06-12T07:30:00Z");
        var repository = new InMemoryPendingMessageRepository();
        var record = CreateRecord(currentTime.AddMinutes(-2));
        record.Provider = EmailProvider.Mailgun;
        repository.Add(record);
        var sender = new RecordingPendingMessageSender {
            ShouldThrow = true,
            ExceptionToThrow = new Exception("Transient provider failure")
        };
        var attempts = new List<int>();
        var delays = new Dictionary<int, TimeSpan> {
            { 1, TimeSpan.FromMinutes(5) },
            { 2, TimeSpan.FromMinutes(10) }
        };
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.Mailgun, sender }
        });
        var processor = new PendingMessageProcessor(
            repository,
            factory,
            retryDelaySelector: attempt => {
                attempts.Add(attempt);
                if (delays.TryGetValue(attempt, out var delay)) {
                    return delay;
                }

                return TimeSpan.FromMinutes(15);
            },
            clock: () => currentTime,
            maxRetryAttempts: 4);

        await processor.ProcessAsync();

        var storedAfterFirstAttempt = await repository.GetByMessageIdAsync(record.MessageId);
        Assert.NotNull(storedAfterFirstAttempt);
        Assert.Equal(1, storedAfterFirstAttempt!.AttemptCount);
        Assert.Equal(currentTime + delays[1], storedAfterFirstAttempt.NextAttemptAt);
        Assert.Equal(new[] { 1 }, attempts);

        currentTime = storedAfterFirstAttempt.NextAttemptAt.AddMinutes(1);

        await processor.ProcessAsync();

        var storedAfterSecondAttempt = await repository.GetByMessageIdAsync(record.MessageId);
        Assert.NotNull(storedAfterSecondAttempt);
        Assert.Equal(2, storedAfterSecondAttempt!.AttemptCount);
        Assert.Equal(currentTime + delays[2], storedAfterSecondAttempt.NextAttemptAt);
        Assert.Equal(new[] { 1, 2 }, attempts);

        currentTime = storedAfterSecondAttempt.NextAttemptAt.AddMinutes(1);
        sender.ShouldThrow = false;

        await processor.ProcessAsync();

        Assert.False(repository.Contains(record.MessageId));
        Assert.Equal(3, sender.SentRecords.Count);
        var lastRecord = sender.SentRecords[sender.SentRecords.Count - 1];
        Assert.Equal(3, lastRecord.AttemptCount);
        Assert.Equal(EmailProvider.Mailgun, lastRecord.Provider);
        Assert.Equal(new[] { 1, 2 }, attempts);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotDoubleSendWhenTwoProcessorsRace() {
        var currentTime = DateTimeOffset.Parse("2024-06-13T09:00:00Z");
        var repository = new CoordinatedPendingMessageRepository(currentTime.AddMinutes(-1));
        var sender = new BlockingPendingMessageSender();
        var observer = new RecordingObserver();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.None, sender }
        });
        var first = new PendingMessageProcessor(repository, factory, clock: () => currentTime, observer: observer);
        var second = new PendingMessageProcessor(repository, factory, clock: () => currentTime, observer: observer);

        var firstTask = first.ProcessAsync();
        var secondTask = second.ProcessAsync();

        repository.ReleaseEnumerations();
        await sender.WaitForFirstSendAsync();
        sender.Release();
        await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(1, sender.SendCount);
        Assert.Contains(observer.Skipped, skipped => skipped.Reason == PendingMessageSkipReason.LeaseNotAcquired);
    }

    private sealed class CoordinatedPendingMessageRepository : IPendingMessageRepository {
        private readonly object syncRoot = new();
        private readonly PendingMessageRecord record;
        private readonly TaskCompletionSource<bool> firstEnumeratorReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> secondEnumeratorReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> firstSnapshotCaptured = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> secondSnapshotCaptured = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> releaseEnumerators = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal CoordinatedPendingMessageRepository(DateTimeOffset nextAttemptAt, int attemptCount = 0) {
            record = CreateRecord(nextAttemptAt, "coordinated-message");
            record.AttemptCount = attemptCount;
        }

        public Task SaveAsync(PendingMessageRecord updatedRecord, CancellationToken cancellationToken = default) {
            lock (syncRoot) {
                record.AttemptCount = updatedRecord.AttemptCount;
                record.NextAttemptAt = updatedRecord.NextAttemptAt;
                record.Timestamp = updatedRecord.Timestamp;
                record.MimeMessage = updatedRecord.MimeMessage;
                record.Server = updatedRecord.Server;
                record.Port = updatedRecord.Port;
                record.UserName = updatedRecord.UserName;
                record.Password = updatedRecord.Password;
                record.Provider = updatedRecord.Provider;
                record.ProviderData = new Dictionary<string, string>(updatedRecord.ProviderData);
            }

            return Task.CompletedTask;
        }

        public Task<PendingMessageRecord?> TryAcquireLeaseAsync(
            string messageId,
            DateTimeOffset dueBeforeOrAt,
            DateTimeOffset leaseUntil,
            CancellationToken cancellationToken = default) {
            lock (syncRoot) {
                if (!string.Equals(record.MessageId, messageId, StringComparison.OrdinalIgnoreCase) || record.NextAttemptAt > dueBeforeOrAt) {
                    return Task.FromResult<PendingMessageRecord?>(null);
                }

                record.NextAttemptAt = leaseUntil;
                return Task.FromResult<PendingMessageRecord?>(record.Clone());
            }
        }

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) {
            lock (syncRoot) {
                return Task.FromResult<PendingMessageRecord?>(
                    string.Equals(record.MessageId, messageId, StringComparison.OrdinalIgnoreCase) ? record.Clone() : null);
            }
        }

        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var isFirstEnumerator = !firstEnumeratorReached.Task.IsCompleted;
            if (isFirstEnumerator) {
                firstEnumeratorReached.TrySetResult(true);
            } else {
                secondEnumeratorReached.TrySetResult(true);
            }

            await Task.WhenAll(firstEnumeratorReached.Task, secondEnumeratorReached.Task, releaseEnumerators.Task).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = record.Clone();
            if (isFirstEnumerator) {
                firstSnapshotCaptured.TrySetResult(true);
            } else {
                secondSnapshotCaptured.TrySetResult(true);
            }

            await Task.WhenAll(firstSnapshotCaptured.Task, secondSnapshotCaptured.Task).ConfigureAwait(false);
            yield return snapshot;
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        internal void ReleaseEnumerations() => releaseEnumerators.TrySetResult(true);
    }

    private sealed class BlockingPendingMessageSender : IPendingMessageSender {
        private readonly TaskCompletionSource<bool> sendStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int SendCount { get; private set; }

        public async Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
            SendCount++;
            sendStarted.TrySetResult(true);
            await release.Task.ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
        }

        internal Task WaitForFirstSendAsync() => sendStarted.Task;

        internal void Release() => release.TrySetResult(true);
    }
}
