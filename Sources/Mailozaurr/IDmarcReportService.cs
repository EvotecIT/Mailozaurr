using Mailozaurr.DmarcReports;

namespace Mailozaurr;

/// <summary>
/// Defines operations for searching DMARC reports.
/// </summary>
public interface IDmarcReportService {
    Task<IList<DmarcReport>> SearchAsync(
        DateTime? since = null,
        DateTime? before = null,
        string? domain = null,
        int maxResults = 0,
        CancellationToken cancellationToken = default);
}
