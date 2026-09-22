using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Processes pending messages by dispatching them through provider specific senders.
/// </summary>
public sealed class PendingMessageProcessor {
    private static readonly TimeSpan MinimumLeaseDuration = TimeSpan.FromSeconds(30);
    private readonly IPendingMessageRepository repository;
    private readonly PendingMessageSenderFactory senderFactory;
    private readonly Func<int, TimeSpan> retryDelaySelector;
    private readonly Func<DateTimeOffset> clock;
    private readonly int maxRetryAttempts;
    private readonly InternalLogger? logger;
    private readonly IPendingMessageProcessorObserver observer;
    private readonly IPendingMessageDeadLetterRepository? deadLetterRepository;
    private readonly Func<Exception, bool> permanentFailureDetector;
    private readonly TimeSpan processingLeaseDuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingMessageProcessor"/> class.
    /// </summary>
    /// <param name="repository">Repository used to load and persist pending messages.</param>
    /// <param name="senderFactory">Factory used to resolve the correct sender for each record.</param>
    /// <param name="retryDelaySelector">Provides the delay applied before the next retry attempt.</param>
    /// <param name="clock">Supplies time for due-message selection, retry scheduling, and outcome timestamps. Processing leases use UTC wall time.</param>
    /// <param name="maxRetryAttempts">Maximum number of delivery attempts performed before giving up on a message.</param>
    /// <param name="logger">Optional logger used to record processing diagnostics.</param>
    /// <param name="observer">Optional observer used to emit telemetry about processing outcomes.</param>
    /// <param name="deadLetterRepository">Optional repository used to retain terminal failures for inspection.</param>
    /// <param name="permanentFailureDetector">Optional delegate that classifies whether a failure should skip retries.</param>
    /// <param name="processingLeaseDuration">
    /// Optional duration used to lease records while they are being processed to avoid concurrent handling.
    /// <see cref="TimeSpan.Zero"/> uses a minimum safety lease of 30 seconds.
    /// </param>
    public PendingMessageProcessor(
        IPendingMessageRepository repository,
        PendingMessageSenderFactory? senderFactory = null,
        Func<int, TimeSpan>? retryDelaySelector = null,
        Func<DateTimeOffset>? clock = null,
        int maxRetryAttempts = 5,
        InternalLogger? logger = null,
        IPendingMessageProcessorObserver? observer = null,
        IPendingMessageDeadLetterRepository? deadLetterRepository = null,
        Func<Exception, bool>? permanentFailureDetector = null,
        TimeSpan? processingLeaseDuration = null) {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.senderFactory = senderFactory ?? new PendingMessageSenderFactory();
        this.retryDelaySelector = retryDelaySelector ?? DefaultRetryDelaySelector;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        if (maxRetryAttempts <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxRetryAttempts), "Maximum retry attempts must be greater than zero.");
        }

        this.maxRetryAttempts = maxRetryAttempts;
        this.logger = logger;
        this.observer = observer ?? NullPendingMessageProcessorObserver.Instance;
        this.deadLetterRepository = deadLetterRepository;
        this.permanentFailureDetector = permanentFailureDetector ?? DefaultPermanentFailureDetector;
        processingLeaseDuration ??= TimeSpan.FromMinutes(1);
        if (processingLeaseDuration < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(processingLeaseDuration), "Processing lease duration cannot be negative.");
        }

        this.processingLeaseDuration = processingLeaseDuration.Value;
    }

    /// <summary>
    /// Attempts to send all pending messages that are due for processing.
    /// </summary>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    public async Task ProcessAsync(CancellationToken cancellationToken = default) {
        await using var enumerator = repository.GetAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (await enumerator.MoveNextAsync().ConfigureAwait(false)) {
            cancellationToken.ThrowIfCancellationRequested();
            var record = enumerator.Current;
            if (record == null) {
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.MessageId)) {
                observer.MessageSkipped(record, PendingMessageSkipReason.MissingMessageId);
                continue;
            }

            var now = clock();
            if (record.NextAttemptAt > now) {
                observer.MessageSkipped(record, PendingMessageSkipReason.NotDue);
                continue;
            }

            var leasedRecord = await AcquireProcessingLeaseAsync(record, now, cancellationToken).ConfigureAwait(false);
            if (leasedRecord == null) {
                logger?.WriteVerbose($"Skipping message {record.MessageId} because another processor already acquired the processing lease.");
                observer.MessageSkipped(record, PendingMessageSkipReason.LeaseNotAcquired);
                continue;
            }
            var leaseId = leasedRecord.ProcessingLeaseId;

            if (leasedRecord.DeadLetteredAt.HasValue) {
                await ExecuteWithLeaseRenewalAsync(leasedRecord, cancellationToken,
                    deliveryToken => CompleteDeadLetterAndRemoveAsync(leasedRecord, leaseId, deliveryToken))
                    .ConfigureAwait(false);
                continue;
            }

            if (leasedRecord.DeliveryAcceptedAt.HasValue) {
                // The enumeration can be stale. Always choose cleanup from the
                // record returned by the atomic lease operation.
                await ExecuteWithLeaseRenewalAsync(leasedRecord, cancellationToken,
                    _ => RemoveOwnedAsync(leasedRecord.MessageId, leaseId, CancellationToken.None))
                    .ConfigureAwait(false);
                continue;
            }

            if (leasedRecord.AttemptCount >= maxRetryAttempts) {
                logger?.WriteWarning($"Removing message {leasedRecord.MessageId} after reaching the retry limit ({leasedRecord.AttemptCount}).");
                observer.MessageDropped(leasedRecord, leasedRecord.AttemptCount, PendingMessageDropReason.RetryLimitReached, null);
                await ExecuteWithLeaseRenewalAsync(leasedRecord, cancellationToken,
                    deliveryToken => DeadLetterAndRemoveAsync(leasedRecord, leasedRecord.AttemptCount,
                        PendingMessageDropReason.RetryLimitReached, null, leaseId, deliveryToken))
                    .ConfigureAwait(false);
                continue;
            }

            var originalAttemptCount = leasedRecord.AttemptCount;
            var attempt = leasedRecord.IncrementAttemptCount();
            observer.MessageAttemptStarted(leasedRecord, attempt);
            var cancellationLeaseReleased = false;
            await ExecuteWithLeaseRenewalAsync(leasedRecord, cancellationToken, async deliveryToken => {
                var stopwatch = Stopwatch.StartNew();

                try {
                    var sender = senderFactory.GetSender(leasedRecord);
                    await sender.SendAsync(leasedRecord, deliveryToken).ConfigureAwait(false);
                    stopwatch.Stop();
                } catch (ProcessingLeaseLostException) {
                    stopwatch.Stop();
                    // A different worker may now own this record. The outcome of a
                    // provider request that ignored cancellation is indeterminate.
                    throw;
                } catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested) {
                    stopwatch.Stop();
                    leasedRecord.ExchangeAttemptCount(originalAttemptCount);
                    leasedRecord.NextAttemptAt = ApplyDelay(clock(), TimeSpan.Zero);
                    leasedRecord.ProcessingLeaseUntil = null;
                    leasedRecord.ProcessingLeaseId = null;
                    observer.MessageFailed(leasedRecord, attempt, ex, stopwatch.Elapsed, willRetry: false, retryDelay: null);
                    try {
                        await SaveOwnedAsync(leasedRecord, leaseId, CancellationToken.None).ConfigureAwait(false);
                        cancellationLeaseReleased = true;
                    } catch (Exception saveEx) {
                        logger?.WriteWarning($"Failed to release processing lease for message {leasedRecord.MessageId} after cancellation: {saveEx.Message}");
                    }
                    throw;
                } catch (OperationCanceledException) when (deliveryToken.IsCancellationRequested) {
                    stopwatch.Stop();
                    // Renewal cancellation is reported by ExecuteWithLeaseRenewalAsync.
                    // Keep the fenced record intact until its lease expires.
                    throw;
                } catch (Exception ex) {
                    stopwatch.Stop();
                    var permanentFailure = permanentFailureDetector(ex);
                    var willRetry = !permanentFailure && attempt < maxRetryAttempts;
                    TimeSpan? delay = null;
                    if (willRetry) {
                        delay = NormalizeDelay(retryDelaySelector(attempt));
                        var failureTime = clock();
                        leasedRecord.NextAttemptAt = ApplyDelay(failureTime, delay.Value);
                        leasedRecord.ProcessingLeaseUntil = null;
                        leasedRecord.ProcessingLeaseId = null;
                    }

                    observer.MessageFailed(leasedRecord, attempt, ex, stopwatch.Elapsed, willRetry, delay);

                    if (!willRetry) {
                        logger?.WriteWarning($"Dropping message {leasedRecord.MessageId} due to {(permanentFailure ? "permanent failure" : "exceeding retry attempts")}: {ex.Message}");
                        observer.MessageDropped(leasedRecord, attempt, permanentFailure ? PendingMessageDropReason.PermanentFailure : PendingMessageDropReason.RetryLimitReached, ex);
                        await DeadLetterAndRemoveAsync(leasedRecord, attempt, permanentFailure ? PendingMessageDropReason.PermanentFailure : PendingMessageDropReason.RetryLimitReached, ex, leaseId, deliveryToken).ConfigureAwait(false);
                        return;
                    }

                    logger?.WriteWarning($"Failed to send message {leasedRecord.MessageId}. Scheduling retry #{attempt + 1} at {leasedRecord.NextAttemptAt:O}. Error: {ex.Message}");
                    await SaveOwnedAsync(leasedRecord, leaseId, deliveryToken).ConfigureAwait(false);
                    return;
                }

                // Provider acceptance and queue acknowledgement are separate outcomes.
                // Persist an accepted marker before removing the record so a failed
                // removal cannot turn a successful send into another delivery attempt.
                var acceptedRecord = leasedRecord.Clone();
                acceptedRecord.DeliveryAcceptedAt = clock();
                acceptedRecord.NextAttemptAt = acceptedRecord.DeliveryAcceptedAt.Value;
                try {
                    await SaveOwnedAsync(acceptedRecord, leaseId, CancellationToken.None).ConfigureAwait(false);
                } catch (ProcessingLeaseLostException) {
                    throw;
                } catch (Exception markerFailure) {
                    // Removal may still succeed even when persisting the marker did not.
                    // If both fail, surface the indeterminate acknowledgement explicitly.
                    try {
                        await RemoveOwnedAsync(leasedRecord.MessageId, leaseId, CancellationToken.None).ConfigureAwait(false);
                    } catch (Exception removalFailure) {
                        throw new AggregateException(
                            $"Delivery of '{leasedRecord.MessageId}' was accepted, but queue acknowledgement failed.",
                            markerFailure, removalFailure);
                    }
                    observer.MessageSent(leasedRecord, attempt, stopwatch.Elapsed);
                    return;
                }

                observer.MessageSent(leasedRecord, attempt, stopwatch.Elapsed);
                await RemoveOwnedAsync(leasedRecord.MessageId, leaseId, CancellationToken.None).ConfigureAwait(false);
            }, () => cancellationLeaseReleased).ConfigureAwait(false);
        }
    }

    private async Task<PendingMessageRecord?> AcquireProcessingLeaseAsync(
        PendingMessageRecord record,
        DateTimeOffset now,
        CancellationToken cancellationToken) {
        var leaseDuration = processingLeaseDuration > TimeSpan.Zero ? processingLeaseDuration : MinimumLeaseDuration;
        // Scheduling may use a caller-supplied clock, but repository leases
        // must use the same wall clock as other workers sharing the queue.
        var leaseUntil = ApplyDelay(DateTimeOffset.UtcNow, leaseDuration);
        return await repository.TryAcquireLeaseAsync(record.MessageId, now, leaseUntil, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteWithLeaseRenewalAsync(PendingMessageRecord record,
        CancellationToken cancellationToken, Func<CancellationToken, Task> action,
        Func<bool>? cancellationOutcomeCommitted = null) {
        if (repository is not IPendingMessageLeaseRenewer renewer ||
            repository is not IPendingMessageLeaseCommitter ||
            string.IsNullOrEmpty(record.ProcessingLeaseId)) {
            await action(cancellationToken).ConfigureAwait(false);
            return;
        }

        using var deliveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Caller cancellation stops provider work, but renewal continues until the
        // callback has durably committed or released the lease-owned outcome.
        using var renewalCancellation = new CancellationTokenSource();
        Task<Exception?> renewal = RenewLeaseWhileSendingAsync(
            renewer, record.MessageId, record.ProcessingLeaseId!, record.ProcessingLeaseUntil!.Value,
            deliveryCancellation, renewalCancellation.Token);
        Exception? actionFailure = null;
        Exception? renewalFailure;
        try {
            await action(deliveryCancellation.Token).ConfigureAwait(false);
        } catch (Exception exception) {
            actionFailure = exception;
        } finally {
            renewalCancellation.Cancel();
        }
        renewalFailure = await renewal.ConfigureAwait(false);
        // A successfully completed callback has already committed its lease-owned
        // terminal outcome. A renewal racing with the final removal may then
        // observe that the record is gone, which is no longer a lease loss.
        // A cancellation handler may have already released the lease through a
        // fenced save. Renewal can then observe that release as a failed renew.
        if (renewalFailure != null && actionFailure != null &&
            !(cancellationToken.IsCancellationRequested && actionFailure is OperationCanceledException &&
              cancellationOutcomeCommitted?.Invoke() == true)) {
            throw new ProcessingLeaseLostException(record.MessageId, renewalFailure);
        }
        if (actionFailure != null) ExceptionDispatchInfo.Capture(actionFailure).Throw();
    }

    private async Task<Exception?> RenewLeaseWhileSendingAsync(
        IPendingMessageLeaseRenewer renewer, string messageId, string leaseId, DateTimeOffset initialLeaseUntil,
        CancellationTokenSource deliveryCancellation, CancellationToken renewalCancellation) {
        var duration = processingLeaseDuration > TimeSpan.Zero ? processingLeaseDuration : MinimumLeaseDuration;
        var interval = TimeSpan.FromTicks(Math.Min(duration.Ticks / 3, TimeSpan.FromSeconds(30).Ticks));
        if (interval <= TimeSpan.Zero) interval = TimeSpan.FromTicks(1);
        var expected = initialLeaseUntil;
        try {
            while (true) {
                await Task.Delay(interval, renewalCancellation).ConfigureAwait(false);
                var next = ApplyDelay(DateTimeOffset.UtcNow, duration);
                if (next <= expected) next = ApplyDelay(expected, duration);
                DateTimeOffset? committed = null;
                if (next > expected) {
                    if (renewer is IPendingMessageLeaseExpirationRenewer expirationRenewer) {
                        committed = await expirationRenewer.TryRenewLeaseAndGetExpirationAsync(
                            messageId, leaseId, expected, next, CancellationToken.None).ConfigureAwait(false);
                    } else if (await renewer.TryRenewLeaseAsync(
                                   messageId, leaseId, expected, next, CancellationToken.None).ConfigureAwait(false)) {
                        committed = next;
                    }
                }
                if (committed == null) {
                    deliveryCancellation.Cancel();
                    return new InvalidOperationException("The processing lease could not be extended.");
                }
                expected = committed.Value;
            }
        } catch (OperationCanceledException) when (renewalCancellation.IsCancellationRequested) {
            return null;
        } catch (Exception exception) {
            deliveryCancellation.Cancel();
            return exception;
        }
    }

    private async Task DeadLetterAndRemoveAsync(
        PendingMessageRecord record,
        int attempt,
        PendingMessageDropReason reason,
        Exception? exception,
        string? leaseId,
        CancellationToken cancellationToken) {
        record.DeadLetteredAt ??= clock();
        record.DeadLetterReason = reason;
        record.DeadLetterAttempt = attempt;
        record.DeadLetterExceptionType = exception?.GetType().FullName;
        record.DeadLetterErrorMessage = exception?.Message;
        await SaveOwnedAsync(record, leaseId, cancellationToken).ConfigureAwait(false);
        await CompleteDeadLetterAndRemoveAsync(record, leaseId, cancellationToken).ConfigureAwait(false);
    }

    private async Task CompleteDeadLetterAndRemoveAsync(
        PendingMessageRecord record,
        string? leaseId,
        CancellationToken cancellationToken) {
        if (deadLetterRepository != null) {
            await deadLetterRepository.SaveAsync(new PendingMessageDeadLetterRecord {
                Message = record.Clone(),
                Reason = record.DeadLetterReason ?? PendingMessageDropReason.RetryLimitReached,
                Attempt = record.DeadLetterAttempt ?? record.AttemptCount,
                DeadLetteredAt = record.DeadLetteredAt ?? clock(),
                ExceptionType = record.DeadLetterExceptionType,
                ErrorMessage = record.DeadLetterErrorMessage
            }, cancellationToken).ConfigureAwait(false);
        }

        // Once the terminal marker is committed, cleanup is safe to attempt even
        // if renewal concurrently cancels provider work. The fenced remove still
        // prevents deleting a record acquired by another worker.
        await RemoveOwnedAsync(record.MessageId, leaseId, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task SaveOwnedAsync(PendingMessageRecord record, string? leaseId,
        CancellationToken cancellationToken) {
        if (repository is IPendingMessageLeaseCommitter committer && !string.IsNullOrEmpty(leaseId)) {
            if (!await committer.TrySaveWithLeaseAsync(record, leaseId!, cancellationToken).ConfigureAwait(false)) {
                throw new ProcessingLeaseLostException(record.MessageId);
            }
            return;
        }
        await repository.SaveAsync(record, cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveOwnedAsync(string messageId, string? leaseId,
        CancellationToken cancellationToken) {
        if (repository is IPendingMessageLeaseCommitter committer && !string.IsNullOrEmpty(leaseId)) {
            if (!await committer.TryRemoveWithLeaseAsync(messageId, leaseId!, cancellationToken).ConfigureAwait(false)) {
                throw new ProcessingLeaseLostException(messageId);
            }
            return;
        }
        await repository.RemoveAsync(messageId, cancellationToken).ConfigureAwait(false);
    }

    private sealed class ProcessingLeaseLostException : InvalidOperationException {
        internal ProcessingLeaseLostException(string messageId, Exception? innerException = null)
            : base($"The processing lease for message '{messageId}' is no longer owned by this worker.",
                innerException) { }
    }

    private static TimeSpan DefaultRetryDelaySelector(int attempt) {
        var exponent = Math.Max(0, attempt - 1);
        var minutes = Math.Pow(2, exponent);
        var capped = Math.Min(60, minutes);
        return TimeSpan.FromMinutes(capped);
    }

    private static TimeSpan NormalizeDelay(TimeSpan delay) {
        if (delay <= TimeSpan.Zero || delay == TimeSpan.MinValue) {
            return TimeSpan.Zero;
        }

        return delay;
    }

    private static bool DefaultPermanentFailureDetector(Exception exception) =>
        exception is InvalidOperationException or ArgumentException;

    private static DateTimeOffset ApplyDelay(DateTimeOffset reference, TimeSpan delay) {
        if (delay <= TimeSpan.Zero) {
            return reference;
        }

        var maxIncrement = DateTimeOffset.MaxValue - reference;
        if (delay > maxIncrement) {
            return DateTimeOffset.MaxValue;
        }

        return reference + delay;
    }

    private sealed class NullPendingMessageProcessorObserver : IPendingMessageProcessorObserver {
        internal static NullPendingMessageProcessorObserver Instance { get; } = new();

        private NullPendingMessageProcessorObserver() {
        }

        public void MessageSkipped(PendingMessageRecord record, PendingMessageSkipReason reason) {
        }

        public void MessageAttemptStarted(PendingMessageRecord record, int attempt) {
        }

        public void MessageSent(PendingMessageRecord record, int attempt, TimeSpan duration) {
        }

        public void MessageFailed(
            PendingMessageRecord record,
            int attempt,
            Exception exception,
            TimeSpan duration,
            bool willRetry,
            TimeSpan? retryDelay) {
        }

        public void MessageDropped(
            PendingMessageRecord record,
            int attempt,
            PendingMessageDropReason reason,
            Exception? exception) {
        }
    }
}
