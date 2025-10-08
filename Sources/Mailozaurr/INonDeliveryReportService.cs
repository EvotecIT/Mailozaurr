using Mailozaurr.NonDeliveryReports;

namespace Mailozaurr;

/// <summary>
/// Defines operations for searching Non-Delivery Reports (DSNs).
/// </summary>
public interface INonDeliveryReportService {
    /// <summary>Searches for Non-Delivery Reports.</summary>
    /// <param name="since">Optional UTC lower bound for report timestamps.</param>
    /// <param name="before">Optional UTC upper bound for report timestamps.</param>
    /// <param name="recipientContains">Optional string that the recipient should contain.</param>
    /// <param name="messageId">Optional original message id to match.</param>
    /// <param name="maxResults">Maximum number of results to return. Use 0 for unlimited.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<IList<NonDeliveryReportResult>> SearchAsync(
        DateTime? since = null,
        DateTime? before = null,
        string? recipientContains = null,
        string? messageId = null,
        int maxResults = 0,
        CancellationToken cancellationToken = default);
}
