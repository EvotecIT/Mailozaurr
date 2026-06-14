using Mailozaurr.DmarcReports;

namespace Mailozaurr;

/// <summary>
/// Defines operations for searching DMARC reports.
/// </summary>
public interface IDmarcReportService {
    /// <summary>Searches for DMARC aggregate reports.</summary>
    /// <param name="since">Optional UTC lower bound for report timestamps.</param>
    /// <param name="before">Optional UTC upper bound for report timestamps.</param>
    /// <param name="domain">Optional domain filter.</param>
    /// <param name="maxResults">Maximum number of results to return. Use 0 for unlimited.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<IList<DmarcReport>> SearchAsync(
        DateTime? since = null,
        DateTime? before = null,
        string? domain = null,
        int maxResults = 0,
        CancellationToken cancellationToken = default);
}