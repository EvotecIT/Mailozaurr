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

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingMessageProcessor"/> class.
    /// </summary>
    /// <param name="repository">Repository used to load and persist pending messages.</param>
    /// <param name="senderFactory">Factory used to resolve the correct sender for each record.</param>
    /// <param name="retryDelaySelector">Provides the delay applied before the next retry attempt.</param>
    /// <param name="clock">Supplies the current time used for scheduling retries.</param>
    public PendingMessageProcessor(
        IPendingMessageRepository repository,
        PendingMessageSenderFactory? senderFactory = null,
        Func<int, TimeSpan>? retryDelaySelector = null,
        Func<DateTimeOffset>? clock = null) {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.senderFactory = senderFactory ?? new PendingMessageSenderFactory();
        this.retryDelaySelector = retryDelaySelector ?? DefaultRetryDelaySelector;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Attempts to send all pending messages that are due for processing.
    /// </summary>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    public async Task ProcessAsync(CancellationToken cancellationToken = default) {
        await foreach (var record in repository.GetAllAsync(cancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (record == null || string.IsNullOrEmpty(record.MessageId)) {
                continue;
            }

            var now = clock();
            if (record.NextAttemptAt > now) {
                continue;
            }

            var sender = senderFactory.GetSender(record);
            record.AttemptCount++;

            try {
                await sender.SendAsync(record, cancellationToken).ConfigureAwait(false);
                await repository.RemoveAsync(record.MessageId, cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception) {
                var delay = retryDelaySelector(record.AttemptCount);
                if (delay < TimeSpan.Zero) {
                    delay = TimeSpan.Zero;
                }

                var scheduledAt = clock();
                record.NextAttemptAt = scheduledAt + delay;
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
}
