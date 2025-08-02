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
        if (record != null && !string.IsNullOrWhiteSpace(report.FinalRecipient)) {
            var recipients = record.Recipients.Split(',');
            if (!recipients.Any(r => string.Equals(r, report.FinalRecipient, StringComparison.OrdinalIgnoreCase))) {
                return null;
            }
        }
        return record;
    }
}
