using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.DmarcReports;

/// <summary>
/// Provides common functionality for DMARC report services.
/// </summary>
public abstract class DmarcReportServiceBase : IDmarcReportService {
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>
    /// Searches for DMARC reports using implementation specific logic.
    /// </summary>
    public async Task<IList<DmarcReport>> SearchAsync(
        DateTime? since = null,
        DateTime? before = null,
        string? domain = null,
        int maxResults = 0,
        CancellationToken cancellationToken = default) {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            return await SearchInternalAsync(since, before, domain, maxResults, cancellationToken).ConfigureAwait(false);
        } finally {
            gate.Release();
        }
    }

    /// <summary>
    /// Performs the actual search for DMARC reports.
    /// </summary>
    protected abstract Task<IList<DmarcReport>> SearchInternalAsync(
        DateTime? since,
        DateTime? before,
        string? domain,
        int maxResults,
        CancellationToken cancellationToken);
}
