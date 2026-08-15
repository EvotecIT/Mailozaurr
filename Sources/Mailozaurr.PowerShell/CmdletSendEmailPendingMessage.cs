using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Sends pending messages stored in a file-based repository.
/// </summary>
[Cmdlet(VerbsCommunications.Send, "EmailPendingMessage", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public sealed class CmdletSendEmailPendingMessage : AsyncPSCmdlet {
    /// <summary>
    /// Allows tests to override the sender factory used for processing.
    /// </summary>
    public static Func<PendingMessageSenderFactory>? SenderFactoryProvider { get; set; }

    /// <summary>Directory containing pending message log file.</summary>
    [Parameter(Mandatory = true)]
    [Alias("PendingPath")]
    public string? PendingMessagesPath { get; set; }

    /// <summary>
    /// Filters queued messages to the specified provider when supplied.
    /// </summary>
    [Parameter]
    public EmailProvider Provider { get; set; }

    /// <summary>
    /// Identifiers of specific messages that should be retried immediately.
    /// </summary>
    [Parameter]
    public string[]? MessageId { get; set; }

    /// <summary>
    /// Processes all messages regardless of their scheduled retry time.
    /// </summary>
    [Parameter]
    public SwitchParameter ProcessAll { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (!ShouldProcess(PendingMessagesPath!, "Sending pending email messages")) {
            return;
        }
        var options = new PendingMessageRepositoryOptions { DirectoryPath = PendingMessagesPath! };
        var repository = new FilePendingMessageRepository(options);
        var logger = new InternalLogger();
        using var bridge = new InternalLoggerPowerShell(
            logger,
            writeVerboseAction: WriteVerbose,
            writeWarningAction: WriteWarning,
            writeDebugAction: WriteDebug,
            writeErrorAction: WriteError,
            writeProgressAction: WriteProgress,
            writeInformationAction: WriteInformation);

        var filteredIds = NormalizeMessageIds(MessageId);
        if (filteredIds != null) {
            foreach (var id in filteredIds) {
                var record = await repository.GetByMessageIdAsync(id, CancelToken).ConfigureAwait(false);
                if (record == null) {
                    WriteWarning($"Message with id '{id}' was not found in the pending repository.");
                }
            }
        }

        var providerFilter = MyInvocation.BoundParameters.ContainsKey(nameof(Provider));
        var forceProcessing = ProcessAll.IsPresent || filteredIds != null;

        var repositoryView = new FilteredPendingMessageRepository(
            repository,
            filteredIds,
            providerFilter ? Provider : (EmailProvider?)null,
            forceProcessing,
            () => DateTimeOffset.UtcNow);

        var observer = new CmdletPendingMessageObserver(this);
        var senderFactory = SenderFactoryProvider?.Invoke() ?? new PendingMessageSenderFactory(
            new Dictionary<EmailProvider, IPendingMessageSender> {
                [EmailProvider.Gmail] = new GmailPendingMessageSender(),
                [EmailProvider.Graph] = new GraphPendingMessageSender()
            });
        var processor = new PendingMessageProcessor(repositoryView, senderFactory, logger: logger, observer: observer);
        await processor.ProcessAsync(CancelToken).ConfigureAwait(false);
    }

    private static IReadOnlyCollection<string>? NormalizeMessageIds(string[]? ids) {
        if (ids == null || ids.Length == 0) {
            return null;
        }

        var normalized = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? null : normalized;
    }

    private sealed class CmdletPendingMessageObserver : IPendingMessageProcessorObserver {
        private readonly CmdletSendEmailPendingMessage _cmdlet;

        internal CmdletPendingMessageObserver(CmdletSendEmailPendingMessage cmdlet) {
            _cmdlet = cmdlet;
        }

        public void MessageSkipped(PendingMessageRecord record, PendingMessageSkipReason reason) {
            var message = reason switch {
                PendingMessageSkipReason.LeaseNotAcquired => $"Skipping message '{record.MessageId}' because another processor already claimed it.",
                _ => $"Skipping message '{record.MessageId}' ({reason})."
            };
            _cmdlet.WriteVerbose(message);
        }

        public void MessageAttemptStarted(PendingMessageRecord record, int attempt) {
            _cmdlet.WriteVerbose($"Sending pending message '{record.MessageId}' (attempt {attempt}).");
        }

        public void MessageSent(PendingMessageRecord record, int attempt, TimeSpan duration) {
            _cmdlet.WriteVerbose($"Message '{record.MessageId}' delivered in {duration.TotalMilliseconds:N0} ms.");
        }

        public void MessageFailed(
            PendingMessageRecord record,
            int attempt,
            Exception exception,
            TimeSpan duration,
            bool willRetry,
            TimeSpan? retryDelay) {
            var retryText = willRetry
                ? retryDelay.HasValue
                    ? $"Retry scheduled in {retryDelay.Value.TotalSeconds:N0} seconds."
                    : "Retry scheduled."
                : "No further retries will be attempted.";
            _cmdlet.WriteWarning($"Failed to send message '{record.MessageId}' on attempt {attempt}: {exception.Message} {retryText}");
        }

        public void MessageDropped(
            PendingMessageRecord record,
            int attempt,
            PendingMessageDropReason reason,
            Exception? exception) {
            var message = $"Message '{record.MessageId}' removed from the queue ({reason}).";
            if (exception != null) {
                message += $" Error: {exception.Message}";
            }
            _cmdlet.WriteWarning(message);
        }
    }

    private sealed class FilteredPendingMessageRepository : IPendingMessageRepository {
        private readonly IPendingMessageRepository _inner;
        private readonly HashSet<string>? _messageIds;
        private readonly EmailProvider? _provider;
        private readonly bool _forceProcessing;
        private readonly Func<DateTimeOffset> _clock;

        internal FilteredPendingMessageRepository(
            IPendingMessageRepository inner,
            IReadOnlyCollection<string>? messageIds,
            EmailProvider? provider,
            bool forceProcessing,
            Func<DateTimeOffset> clock) {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _messageIds = messageIds != null
                ? new HashSet<string>(messageIds, StringComparer.OrdinalIgnoreCase)
                : null;
            _provider = provider;
            _forceProcessing = forceProcessing;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) =>
            _inner.SaveAsync(record, cancellationToken);

        public async Task<PendingMessageRecord?> TryAcquireLeaseAsync(
            string messageId,
            DateTimeOffset dueBeforeOrAt,
            DateTimeOffset leaseUntil,
            CancellationToken cancellationToken = default) {
            var record = await _inner.GetByMessageIdAsync(messageId, cancellationToken).ConfigureAwait(false);
            if (record == null) {
                return null;
            }

            if (_messageIds != null && !ContainsMessageId(record.MessageId)) {
                return null;
            }

            if (_provider.HasValue && record.Provider != _provider.Value) {
                return null;
            }

            var effectiveDueTime = _forceProcessing ? DateTimeOffset.MaxValue : dueBeforeOrAt;
            return await _inner.TryAcquireLeaseAsync(messageId, effectiveDueTime, leaseUntil, cancellationToken).ConfigureAwait(false);
        }

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) =>
            _inner.GetByMessageIdAsync(messageId, cancellationToken);

        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var now = _clock();
            await foreach (var record in _inner.GetAllAsync(cancellationToken).ConfigureAwait(false)) {
                cancellationToken.ThrowIfCancellationRequested();
                if (record == null) {
                    continue;
                }

                if (_messageIds != null && !ContainsMessageId(record.MessageId)) {
                    continue;
                }

                if (_provider.HasValue && record.Provider != _provider.Value) {
                    continue;
                }

                if (_forceProcessing && record.NextAttemptAt > now) {
                    var forcedRecord = record.Clone();
                    forcedRecord.NextAttemptAt = now;
                    yield return forcedRecord;
                    continue;
                }

                yield return record;
            }
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) =>
            _inner.RemoveAsync(messageId, cancellationToken);

        private bool ContainsMessageId(string? id) {
            if (string.IsNullOrEmpty(id) || _messageIds == null) {
                return false;
            }
            return _messageIds.Contains(id!);
        }
    }
}
