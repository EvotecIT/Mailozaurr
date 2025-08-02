using Mailozaurr.NonDeliveryReports;

namespace Mailozaurr;

public sealed class SendLogResolver {
    private readonly ISentMessageRepository repository;

    public SendLogResolver(ISentMessageRepository repository) => this.repository = repository;

    public async Task<SentMessageRecord?> ResolveAsync(NonDeliveryReport report, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(report?.OriginalMessageId)) {
            return null;
        }
        var record = await repository.GetByMessageIdAsync(report.OriginalMessageId!, cancellationToken);
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
