using Mailozaurr.NonDeliveryReports;

namespace Mailozaurr;

/// <summary>
/// Service for searching non-delivery reports.
/// </summary>
public interface INonDeliveryReportService {
    /// <summary>
    /// Searches for non-delivery reports matching provided criteria.
    /// </summary>
    /// <param name="since">Start date for the search range.</param>
    /// <param name="before">End date for the search range.</param>
    /// <param name="recipientContains">Filter by recipient substring.</param>
    /// <param name="messageId">Filter by message identifier.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>List of matching non-delivery report results.</returns>
    Task<IList<NonDeliveryReportResult>> SearchAsync(
        DateTime? since = null,
        DateTime? before = null,
        string? recipientContains = null,
        string? messageId = null,
        int maxResults = 0,
        CancellationToken cancellationToken = default);
}
