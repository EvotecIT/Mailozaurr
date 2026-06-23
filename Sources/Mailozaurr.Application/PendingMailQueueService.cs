namespace Mailozaurr.Application;

/// <summary>
/// Reusable queue facade over Mailozaurr pending-message infrastructure.
/// </summary>
public sealed class PendingMailQueueService : IMailQueueService {
    private readonly IPendingMessageRepository _repository;
    private readonly IPendingMessageDeadLetterRepository _deadLetters;
    private readonly Func<IPendingMessageRepository, IPendingMessageDeadLetterRepository, IPendingMessageProcessorObserver, CancellationToken, Task> _processAsync;

    /// <summary>
    /// Creates a new queue service.
    /// </summary>
    public PendingMailQueueService(
        IPendingMessageRepository repository,
        IPendingMessageDeadLetterRepository? deadLetters = null,
        Func<IPendingMessageRepository, IPendingMessageDeadLetterRepository, IPendingMessageProcessorObserver, CancellationToken, Task>? processAsync = null) {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _deadLetters = deadLetters ?? new FilePendingMessageDeadLetterRepository();
        _processAsync = processAsync ?? DefaultProcessAsync;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QueuedMessageSummary>> ListAsync(CancellationToken cancellationToken = default) {
        var messages = new List<QueuedMessageSummary>();
        await foreach (var record in _repository.GetAllAsync(cancellationToken).ConfigureAwait(false)) {
            if (record == null) {
                continue;
            }

            messages.Add(Map(record));
        }

        return messages
            .OrderBy(message => message.NextAttemptAt)
            .ThenBy(message => message.MessageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QueuedMessageCompact>> ListCompactAsync(CancellationToken cancellationToken = default) =>
        (await ListAsync(cancellationToken).ConfigureAwait(false))
        .Select(ToCompact)
        .ToArray();

    /// <inheritdoc />
    public async Task<QueuedMessageSummary?> GetAsync(string messageId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("Message id is required.", nameof(messageId));
        }

        var record = await _repository.GetByMessageIdAsync(messageId.Trim(), cancellationToken).ConfigureAwait(false);
        return record == null ? null : Map(record);
    }

    /// <inheritdoc />
    public async Task<QueuedMessageCompact?> GetCompactAsync(string messageId, CancellationToken cancellationToken = default) {
        var message = await GetAsync(messageId, cancellationToken).ConfigureAwait(false);
        return message == null ? null : ToCompact(message);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QueuedMessageSummary>> ListDeadLettersAsync(CancellationToken cancellationToken = default) {
        var messages = new List<QueuedMessageSummary>();
        await foreach (var record in _deadLetters.GetAllAsync(cancellationToken).ConfigureAwait(false)) {
            messages.Add(MapDeadLetter(record));
        }

        return messages
            .OrderByDescending(message => message.NextAttemptAt)
            .ThenBy(message => message.MessageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<QueuedMessageSummary?> GetDeadLetterAsync(string messageId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("Message id is required.", nameof(messageId));
        }

        var record = await _deadLetters.GetByMessageIdAsync(messageId.Trim(), cancellationToken).ConfigureAwait(false);
        return record == null ? null : MapDeadLetter(record);
    }

    /// <inheritdoc />
    public async Task<OperationResult> RemoveDeadLetterAsync(string messageId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("Message id is required.", nameof(messageId));
        }

        var normalizedMessageId = messageId.Trim();
        var existing = await _deadLetters.GetByMessageIdAsync(normalizedMessageId, cancellationToken).ConfigureAwait(false);
        if (existing == null) {
            return OperationResult.Failure("queue_dead_letter_not_found", $"Dead-lettered message '{normalizedMessageId}' was not found.");
        }

        await _deadLetters.RemoveAsync(normalizedMessageId, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success($"Dead-lettered message '{normalizedMessageId}' removed.");
    }

    /// <inheritdoc />
    public async Task<OperationResult> RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("Message id is required.", nameof(messageId));
        }

        var normalizedMessageId = messageId.Trim();
        var existing = await _repository.GetByMessageIdAsync(normalizedMessageId, cancellationToken).ConfigureAwait(false);
        if (existing == null) {
            return OperationResult.Failure("queue_message_not_found", $"Queued message '{normalizedMessageId}' was not found.");
        }

        await _repository.RemoveAsync(normalizedMessageId, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success($"Queued message '{normalizedMessageId}' removed.");
    }

    /// <inheritdoc />
    public async Task<QueueProcessResult> ProcessAsync(CancellationToken cancellationToken = default) {
        var observer = new CountingObserver();
        await _processAsync(_repository, _deadLetters, observer, cancellationToken).ConfigureAwait(false);
        return observer.CreateResult();
    }

    private static Task DefaultProcessAsync(
        IPendingMessageRepository repository,
        IPendingMessageDeadLetterRepository deadLetters,
        IPendingMessageProcessorObserver observer,
        CancellationToken cancellationToken) {
        var processor = new PendingMessageProcessor(repository, observer: observer, deadLetterRepository: deadLetters);
        return processor.ProcessAsync(cancellationToken);
    }

    private static QueuedMessageSummary Map(PendingMessageRecord record) => new() {
        MessageId = record.MessageId,
        Provider = record.Provider.ToString(),
        ProfileKind = MapProfileKind(record.Provider),
        QueuedAt = record.Timestamp,
        NextAttemptAt = record.NextAttemptAt,
        AttemptCount = record.AttemptCount,
        HasProviderData = record.ProviderData.Count > 0
    };

    private static QueuedMessageSummary MapDeadLetter(PendingMessageDeadLetterRecord record) {
        var summary = Map(record.Message);
        summary.IsDeadLetter = true;
        summary.DeadLetterReason = record.Reason.ToString();
        summary.ErrorMessage = record.ErrorMessage;
        summary.NextAttemptAt = record.DeadLetteredAt;
        return summary;
    }

    private static QueuedMessageCompact ToCompact(QueuedMessageSummary message) => new() {
        MessageId = message.MessageId,
        Provider = message.Provider,
        ProfileKind = message.ProfileKind,
        NextAttemptAt = message.NextAttemptAt,
        AttemptCount = message.AttemptCount,
        IsDue = message.NextAttemptAt <= DateTimeOffset.UtcNow,
        HasProviderData = message.HasProviderData,
        IsDeadLetter = message.IsDeadLetter,
        DeadLetterReason = message.DeadLetterReason,
        ErrorMessage = message.ErrorMessage,
        Summary = message.IsDeadLetter
            ? $"{message.MessageId} [{message.Provider}] dead-letter={message.DeadLetterReason} error={message.ErrorMessage ?? "(none)"}"
            : $"{message.MessageId} [{message.Provider}] attempts={message.AttemptCount} next={message.NextAttemptAt:O}"
    };

    private static MailProfileKind MapProfileKind(EmailProvider provider) => provider switch {
        EmailProvider.Graph => MailProfileKind.Graph,
        EmailProvider.Gmail => MailProfileKind.Gmail,
        EmailProvider.SendGrid => MailProfileKind.SendGrid,
        EmailProvider.Mailgun => MailProfileKind.Mailgun,
        EmailProvider.SES => MailProfileKind.Ses,
        _ => MailProfileKind.Smtp
    };

    private sealed class CountingObserver : IPendingMessageProcessorObserver {
        private readonly QueueProcessResult _result = new() {
            Succeeded = true,
            Message = "Queue processing completed."
        };

        public void MessageSkipped(PendingMessageRecord record, PendingMessageSkipReason reason) {
            _result.SkippedCount++;
        }

        public void MessageAttemptStarted(PendingMessageRecord record, int attempt) {
            _result.AttemptedCount++;
        }

        public void MessageSent(PendingMessageRecord record, int attempt, TimeSpan duration) {
            _result.SentCount++;
        }

        public void MessageFailed(
            PendingMessageRecord record,
            int attempt,
            Exception exception,
            TimeSpan duration,
            bool willRetry,
            TimeSpan? retryDelay) {
            _result.FailedCount++;
            _result.Message = exception.Message;
        }

        public void MessageDropped(
            PendingMessageRecord record,
            int attempt,
            PendingMessageDropReason reason,
            Exception? exception) {
            _result.DroppedCount++;
            if (exception != null) {
                _result.Message = exception.Message;
            }
        }

        public QueueProcessResult CreateResult() => _result;
    }
}
