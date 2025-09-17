using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Processes pending messages by dispatching them through provider specific senders.
/// </summary>
public sealed class PendingMessageProcessor {
    private readonly IPendingMessageRepository repository;
    private readonly PendingMessageSenderFactory senderFactory;
    private readonly Func<int, TimeSpan> retryDelaySelector;
    private readonly Func<DateTimeOffset> clock;
    private readonly int maxRetryAttempts;
    private readonly InternalLogger? logger;
    private readonly IPendingMessageProcessorObserver observer;
    private readonly Func<Exception, bool> permanentFailureDetector;

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingMessageProcessor"/> class.
    /// </summary>
    /// <param name="repository">Repository used to load and persist pending messages.</param>
    /// <param name="senderFactory">Factory used to resolve the correct sender for each record.</param>
    /// <param name="retryDelaySelector">Provides the delay applied before the next retry attempt.</param>
    /// <param name="clock">Supplies the current time used for scheduling retries.</param>
    /// <param name="maxRetryAttempts">Maximum number of delivery attempts performed before giving up on a message.</param>
    /// <param name="logger">Optional logger used to record processing diagnostics.</param>
    /// <param name="observer">Optional observer used to emit telemetry about processing outcomes.</param>
    /// <param name="permanentFailureDetector">Optional delegate that classifies whether a failure should skip retries.</param>
    public PendingMessageProcessor(
        IPendingMessageRepository repository,
        PendingMessageSenderFactory? senderFactory = null,
        Func<int, TimeSpan>? retryDelaySelector = null,
        Func<DateTimeOffset>? clock = null,
        int maxRetryAttempts = 5,
        InternalLogger? logger = null,
        IPendingMessageProcessorObserver? observer = null,
        Func<Exception, bool>? permanentFailureDetector = null) {
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
        this.permanentFailureDetector = permanentFailureDetector ?? DefaultPermanentFailureDetector;
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

            if (record.AttemptCount >= maxRetryAttempts) {
                logger?.WriteWarning($"Removing message {record.MessageId} after reaching the retry limit ({record.AttemptCount}).");
                observer.MessageDropped(record, record.AttemptCount, PendingMessageDropReason.RetryLimitReached, null);
                await repository.RemoveAsync(record.MessageId, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var sender = senderFactory.GetSender(record);
            var attempt = record.IncrementAttemptCount();
            observer.MessageAttemptStarted(record, attempt);
            var stopwatch = Stopwatch.StartNew();

            try {
                await sender.SendAsync(record, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();
                observer.MessageSent(record, attempt, stopwatch.Elapsed);
                await repository.RemoveAsync(record.MessageId, cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException ex) {
                stopwatch.Stop();
                observer.MessageFailed(record, attempt, ex, stopwatch.Elapsed, willRetry: false, retryDelay: null);
                record.AttemptCount = attempt - 1;
                throw;
            } catch (Exception ex) {
                stopwatch.Stop();
                var permanentFailure = permanentFailureDetector(ex);
                var willRetry = !permanentFailure && attempt < maxRetryAttempts;
                TimeSpan? delay = null;
                if (willRetry) {
                    delay = NormalizeDelay(retryDelaySelector(attempt));
                    record.NextAttemptAt = now + delay.Value;
                }

                observer.MessageFailed(record, attempt, ex, stopwatch.Elapsed, willRetry, delay);

                if (!willRetry) {
                    logger?.WriteWarning($"Dropping message {record.MessageId} due to {(permanentFailure ? "permanent failure" : "exceeding retry attempts")}: {ex.Message}");
                    observer.MessageDropped(record, attempt, permanentFailure ? PendingMessageDropReason.PermanentFailure : PendingMessageDropReason.RetryLimitReached, ex);
                    await repository.RemoveAsync(record.MessageId, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                logger?.WriteWarning($"Failed to send message {record.MessageId}. Scheduling retry #{attempt + 1} at {record.NextAttemptAt:O}. Error: {ex.Message}");
                await repository.SaveAsync(record, cancellationToken).ConfigureAwait(false);
            }
        }
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
