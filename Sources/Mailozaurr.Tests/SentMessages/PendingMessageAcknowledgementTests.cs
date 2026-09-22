using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Mailozaurr.Tests;

public sealed class PendingMessageAcknowledgementTests {
    [Fact]
    public async Task RemovalFailureAfterAcceptanceDoesNotSendAgain() {
        var now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var repository = new FailingRemovalRepository(new PendingMessageRecord {
            MessageId = "accepted-message",
            Timestamp = now,
            NextAttemptAt = now
        });
        var sender = new CountingSender();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            [EmailProvider.None] = sender
        });
        var processor = new PendingMessageProcessor(repository, factory, clock: () => now);

        await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync());
        Assert.Equal(1, sender.SendCount);
        Assert.NotNull(repository.Record?.DeliveryAcceptedAt);

        await processor.ProcessAsync();
        Assert.Equal(1, sender.SendCount);
        Assert.Null(repository.Record);
    }

    [Fact]
    public async Task SuccessfulFallbackRemovalReportsAcceptedDelivery() {
        var now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var repository = new FailingRemovalRepository(new PendingMessageRecord {
            MessageId = "fallback-acknowledged",
            Timestamp = now,
            NextAttemptAt = now
        }) { FailFirstRemoval = false, FailAcceptedMarker = true };
        var sender = new CountingSender();
        var observer = new CountingObserver();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            [EmailProvider.None] = sender
        });

        await new PendingMessageProcessor(repository, factory, observer: observer, clock: () => now).ProcessAsync();

        Assert.Equal(1, sender.SendCount);
        Assert.Equal(1, observer.SentCount);
        Assert.Null(repository.Record);
    }

    [Fact]
    public async Task LeaseRenewalContinuesUntilAcceptanceIsPersisted() {
        var now = DateTimeOffset.UtcNow;
        var repository = new SlowAcceptanceRepository(new PendingMessageRecord {
            MessageId = "slow-acceptance",
            Timestamp = now,
            NextAttemptAt = now
        });
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            [EmailProvider.None] = new CountingSender()
        });

        await new PendingMessageProcessor(repository, factory,
            processingLeaseDuration: TimeSpan.FromMilliseconds(90)).ProcessAsync();

        Assert.True(repository.RenewCount > 0);
        Assert.Null(repository.Record);
    }

    [Fact]
    public async Task CancellationAfterFencedLeaseReleaseRemainsCancellation() {
        var now = DateTimeOffset.UtcNow;
        var repository = new SlowAcceptanceRepository(new PendingMessageRecord {
            MessageId = "cancel-after-release",
            Timestamp = now,
            NextAttemptAt = now
        }) { DelayAfterRelease = TimeSpan.FromMilliseconds(150) };
        using var cancellation = new CancellationTokenSource();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            [EmailProvider.None] = new CancelingSender(cancellation)
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new PendingMessageProcessor(repository, factory,
                processingLeaseDuration: TimeSpan.FromMilliseconds(90)).ProcessAsync(cancellation.Token));

        Assert.True(repository.RenewRejectedCount > 0);
        Assert.Null(repository.Record?.ProcessingLeaseId);
        Assert.Equal(0, repository.Record?.AttemptCount);
    }

    [Fact]
    public async Task FailedRenewalDoesNotMasqueradeAsCallerCancellation() {
        var now = DateTimeOffset.UtcNow;
        var repository = new SlowAcceptanceRepository(new PendingMessageRecord {
            MessageId = "renewal-failed", Timestamp = now, NextAttemptAt = now
        }) { RejectRenewal = true };
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            [EmailProvider.None] = new WaitForCancellationSender()
        });

        var failure = await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            new PendingMessageProcessor(repository, factory,
                processingLeaseDuration: TimeSpan.FromMilliseconds(90)).ProcessAsync());

        Assert.Contains("processing lease", failure.Message);
        Assert.True(repository.RenewRejectedCount > 0);
        Assert.NotNull(repository.Record?.ProcessingLeaseId);
    }

    [Fact]
    public async Task CleanupOfPreviouslyAcceptedRecordDoesNotCountAsAnotherAttempt() {
        var now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var repository = new FailingRemovalRepository(new PendingMessageRecord {
            MessageId = "previously-accepted",
            Timestamp = now,
            NextAttemptAt = now,
            AttemptCount = 5,
            DeliveryAcceptedAt = now.AddMinutes(-1)
        }) { FailFirstRemoval = false };
        var sender = new CountingSender();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            [EmailProvider.None] = sender
        });
        var processor = new PendingMessageProcessor(repository, factory, clock: () => now,
            maxRetryAttempts: 5);

        await processor.ProcessAsync();

        Assert.Equal(0, sender.SendCount);
        Assert.Null(repository.Record);
    }

    [Fact]
    public async Task AcceptedStateFromLeaseWinsOverStaleEnumeration() {
        var now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var current = new PendingMessageRecord {
            MessageId = "accepted-after-snapshot",
            Timestamp = now,
            NextAttemptAt = now,
            DeliveryAcceptedAt = now
        };
        var repository = new FailingRemovalRepository(current) {
            FailFirstRemoval = false,
            EnumerationRecord = new PendingMessageRecord {
                MessageId = current.MessageId,
                Timestamp = now,
                NextAttemptAt = now
            }
        };
        var sender = new CountingSender();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            [EmailProvider.None] = sender
        });

        await new PendingMessageProcessor(repository, factory, clock: () => now).ProcessAsync();

        Assert.Equal(0, sender.SendCount);
        Assert.Null(repository.Record);
    }

    private sealed class CountingSender : IPendingMessageSender {
        public int SendCount { get; private set; }

        public Task SendAsync(PendingMessageRecord record, CancellationToken cancellationToken) {
            SendCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class CancelingSender : IPendingMessageSender {
        private readonly CancellationTokenSource cancellation;

        internal CancelingSender(CancellationTokenSource cancellation) => this.cancellation = cancellation;

        public Task SendAsync(PendingMessageRecord record, CancellationToken cancellationToken) {
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class WaitForCancellationSender : IPendingMessageSender {
        public async Task SendAsync(PendingMessageRecord record, CancellationToken cancellationToken) =>
            await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private sealed class CountingObserver : IPendingMessageProcessorObserver {
        public int SentCount { get; private set; }
        public void MessageSkipped(PendingMessageRecord record, PendingMessageSkipReason reason) { }
        public void MessageAttemptStarted(PendingMessageRecord record, int attempt) { }
        public void MessageSent(PendingMessageRecord record, int attempt, TimeSpan duration) => SentCount++;
        public void MessageFailed(PendingMessageRecord record, int attempt, Exception exception,
            TimeSpan duration, bool willRetry, TimeSpan? retryDelay) { }
        public void MessageDropped(PendingMessageRecord record, int attempt,
            PendingMessageDropReason reason, Exception? exception) { }
    }

    private sealed class FailingRemovalRepository : IPendingMessageRepository {
        public FailingRemovalRepository(PendingMessageRecord record) => Record = record;

        public PendingMessageRecord? Record { get; private set; }
        public bool FailFirstRemoval { get; set; } = true;
        public bool FailAcceptedMarker { get; set; }
        public PendingMessageRecord? EnumerationRecord { get; set; }

        public Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) {
            if (FailAcceptedMarker && record.DeliveryAcceptedAt != null)
                throw new IOException("Simulated acceptance marker failure.");
            Record = record.Clone();
            return Task.CompletedTask;
        }

        public Task<PendingMessageRecord?> TryAcquireLeaseAsync(string messageId,
            DateTimeOffset dueBeforeOrAt, DateTimeOffset leaseUntil,
            CancellationToken cancellationToken = default) {
            if (Record == null || Record.MessageId != messageId || Record.NextAttemptAt > dueBeforeOrAt) {
                return Task.FromResult<PendingMessageRecord?>(null);
            }
            Record.NextAttemptAt = leaseUntil;
            return Task.FromResult<PendingMessageRecord?>(Record.Clone());
        }

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Record?.MessageId == messageId ? Record.Clone() : null);

        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            if (EnumerationRecord != null) yield return EnumerationRecord.Clone();
            else if (Record != null) yield return Record.Clone();
            await Task.CompletedTask;
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
            if (FailFirstRemoval) {
                FailFirstRemoval = false;
                throw new IOException("Simulated queue acknowledgement failure.");
            }
            Record = null;
            return Task.CompletedTask;
        }
    }

    private sealed class SlowAcceptanceRepository : IPendingMessageRepository,
        IPendingMessageLeaseRenewer, IPendingMessageLeaseCommitter {
        internal SlowAcceptanceRepository(PendingMessageRecord record) => Record = record;

        internal PendingMessageRecord? Record { get; private set; }
        internal int RenewCount { get; private set; }
        internal int RenewRejectedCount { get; private set; }
        internal TimeSpan DelayAfterRelease { get; set; }
        internal bool RejectRenewal { get; set; }

        public Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) {
            Record = record.Clone();
            return Task.CompletedTask;
        }

        public Task<PendingMessageRecord?> TryAcquireLeaseAsync(string messageId,
            DateTimeOffset dueBeforeOrAt, DateTimeOffset leaseUntil,
            CancellationToken cancellationToken = default) {
            if (Record == null || Record.MessageId != messageId || Record.NextAttemptAt > dueBeforeOrAt)
                return Task.FromResult<PendingMessageRecord?>(null);
            Record.ProcessingLeaseId = Guid.NewGuid().ToString("N");
            Record.ProcessingLeaseUntil = leaseUntil;
            Record.NextAttemptAt = leaseUntil;
            return Task.FromResult<PendingMessageRecord?>(Record.Clone());
        }

        public Task<bool> TryRenewLeaseAsync(string messageId, string leaseId,
            DateTimeOffset expectedLeaseUntil, DateTimeOffset newLeaseUntil,
            CancellationToken cancellationToken = default) {
            if (RejectRenewal) {
                RenewRejectedCount++;
                return Task.FromResult(false);
            }
            if (Record?.MessageId != messageId || Record.ProcessingLeaseId != leaseId ||
                Record.ProcessingLeaseUntil != expectedLeaseUntil) {
                RenewRejectedCount++;
                return Task.FromResult(false);
            }
            RenewCount++;
            Record.ProcessingLeaseUntil = newLeaseUntil;
            Record.NextAttemptAt = newLeaseUntil;
            return Task.FromResult(true);
        }

        public async Task<bool> TrySaveWithLeaseAsync(PendingMessageRecord record, string leaseId,
            CancellationToken cancellationToken = default) {
            if (record.DeliveryAcceptedAt.HasValue) await Task.Delay(300, cancellationToken);
            if (Record?.ProcessingLeaseId != leaseId) return false;
            if (record.ProcessingLeaseId == null) {
                Record = record.Clone();
                if (DelayAfterRelease > TimeSpan.Zero) await Task.Delay(DelayAfterRelease, cancellationToken);
                return true;
            }
            record.ProcessingLeaseUntil = Record.ProcessingLeaseUntil;
            record.NextAttemptAt = Record.ProcessingLeaseUntil!.Value;
            Record = record.Clone();
            return true;
        }

        public async Task<bool> TryRemoveWithLeaseAsync(string messageId, string leaseId,
            CancellationToken cancellationToken = default) {
            await Task.Delay(150, cancellationToken);
            if (Record?.MessageId != messageId || Record.ProcessingLeaseId != leaseId)
                return false;
            Record = null;
            return true;
        }

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Record?.MessageId == messageId ? Record.Clone() : null);

        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            if (Record != null) yield return Record.Clone();
            await Task.CompletedTask;
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
            if (Record?.MessageId == messageId) Record = null;
            return Task.CompletedTask;
        }
    }
}
