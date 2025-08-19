using Mailozaurr.NonDeliveryReports;

namespace Mailozaurr;

/// <summary>
/// Resolves log entries for previously sent messages based on non-delivery reports.
/// </summary>
public sealed class SendLogResolver {
    private readonly ISentMessageRepository repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendLogResolver"/> class.
    /// </summary>
    /// <param name="repository">Repository used to look up sent messages.</param>
    public SendLogResolver(ISentMessageRepository repository) =>
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <summary>
    /// Attempts to resolve a sent message record from a non-delivery report.
    /// </summary>
    /// <param name="report">Non-delivery report to analyze.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The matching <see cref="SentMessageRecord"/>, or <c>null</c> if not found.</returns>
    public async Task<SentMessageRecord?> ResolveAsync(NonDeliveryReport report, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(report.OriginalMessageId)) {
            return null;
        }
        var messageId = report.OriginalMessageId!;
        var record = await repository.GetByMessageIdAsync(messageId, cancellationToken);
        if (record != null) {
            var recipient = report.FinalRecipientAddress ?? report.OriginalRecipientAddress;
            if (!string.IsNullOrWhiteSpace(recipient)) {
                var recipients = record.Recipients.Split(',').Select(r => r.Trim());
                if (!recipients.Any(r => string.Equals(r, recipient, StringComparison.OrdinalIgnoreCase))) {
                    return null;
                }
            }
        }
        return record;
    }
}
