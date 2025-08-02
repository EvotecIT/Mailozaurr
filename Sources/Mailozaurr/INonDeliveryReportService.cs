using Mailozaurr.NonDeliveryReports;

namespace Mailozaurr;

public interface INonDeliveryReportService {
    Task<IList<NonDeliveryReportResult>> SearchAsync(
        DateTime? since = null,
        DateTime? before = null,
        string? recipientContains = null,
        string? messageId = null,
        int maxResults = 0,
        CancellationToken cancellationToken = default);
}
