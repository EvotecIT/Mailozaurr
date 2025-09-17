using System;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingMessageProcessor"/> class.
    /// </summary>
    /// <param name="repository">Repository used to load and persist pending messages.</param>
    /// <param name="senderFactory">Factory used to resolve the correct sender for each record.</param>
    /// <param name="retryDelaySelector">Provides the delay applied before the next retry attempt.</param>
    /// <param name="clock">Supplies the current time used for scheduling retries.</param>
    /// <param name="maxRetryAttempts">Maximum number of delivery attempts performed before giving up on a message.</param>
    /// <param name="logger">Optional logger used to record processing diagnostics.</param>
    public PendingMessageProcessor(
        IPendingMessageRepository repository,
        PendingMessageSenderFactory? senderFactory = null,
        Func<int, TimeSpan>? retryDelaySelector = null,
        Func<DateTimeOffset>? clock = null,
        int maxRetryAttempts = 5,
        InternalLogger? logger = null) {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.senderFactory = senderFactory ?? new PendingMessageSenderFactory();
        this.retryDelaySelector = retryDelaySelector ?? DefaultRetryDelaySelector;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        if (maxRetryAttempts <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxRetryAttempts), "Maximum retry attempts must be greater than zero.");
        }

        this.maxRetryAttempts = maxRetryAttempts;
        this.logger = logger;
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
            if (record == null || string.IsNullOrWhiteSpace(record.MessageId)) {
                continue;
            }

            var now = clock();
            if (record.NextAttemptAt > now) {
                continue;
            }

            if (record.AttemptCount >= maxRetryAttempts) {
                logger?.WriteWarning($"Removing message {record.MessageId} after reaching the retry limit ({record.AttemptCount}).");
                await repository.RemoveAsync(record.MessageId, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var sender = senderFactory.GetSender(record);
            var attempt = record.IncrementAttemptCount();

            try {
                await sender.SendAsync(record, cancellationToken).ConfigureAwait(false);
                await repository.RemoveAsync(record.MessageId, cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                record.AttemptCount = attempt - 1;
                throw;
            } catch (Exception ex) {
                if (IsPermanentFailure(ex) || attempt >= maxRetryAttempts) {
                    logger?.WriteWarning($"Dropping message {record.MessageId} due to {(IsPermanentFailure(ex) ? "permanent failure" : "exceeding retry attempts")}: {ex.Message}");
                    await repository.RemoveAsync(record.MessageId, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var delay = retryDelaySelector(attempt);
                if (delay < TimeSpan.Zero) {
                    delay = TimeSpan.Zero;
                }

                record.NextAttemptAt = now + delay;
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

    private static bool IsPermanentFailure(Exception exception) =>
        exception is InvalidOperationException or ArgumentException;
}
