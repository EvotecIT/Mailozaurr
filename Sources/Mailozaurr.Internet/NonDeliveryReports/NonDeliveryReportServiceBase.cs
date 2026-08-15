using Mailozaurr;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.NonDeliveryReports;

/// <summary>
/// Provides common functionality for non-delivery report services.
/// </summary>
public abstract class NonDeliveryReportServiceBase : INonDeliveryReportService {
    private readonly SendLogResolver resolver;
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="NonDeliveryReportServiceBase"/> class.
    /// </summary>
    /// <param name="resolver">Resolver used to match reports with sent messages.</param>
    protected NonDeliveryReportServiceBase(SendLogResolver resolver) => this.resolver = resolver;

    /// <summary>
    /// Searches for non-delivery reports using implementation specific logic.
    /// </summary>
    /// <param name="since">Only reports after this date are considered.</param>
    /// <param name="before">Only reports before this date are considered.</param>
    /// <param name="recipientContains">Filters reports by recipient substring.</param>
    /// <param name="messageId">Filters reports by message identifier.</param>
    /// <param name="maxResults">Maximum number of reports to return.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>List of found reports paired with optional sent message records.</returns>
    public async Task<IList<NonDeliveryReportResult>> SearchAsync(
        DateTime? since = null,
        DateTime? before = null,
        string? recipientContains = null,
        string? messageId = null,
        int maxResults = 0,
        CancellationToken cancellationToken = default) {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        IList<NonDeliveryReport> reports;
        try {
            reports = await SearchInternalAsync(since, before, recipientContains, messageId, maxResults, cancellationToken).ConfigureAwait(false);
        } finally {
            gate.Release();
        }
        var results = new List<NonDeliveryReportResult>(reports.Count);
        foreach (NonDeliveryReport report in reports) {
            SentMessageRecord? record = await resolver.ResolveAsync(report, cancellationToken).ConfigureAwait(false);
            results.Add(new NonDeliveryReportResult(report, record));
        }
        return results;
    }

    /// <summary>
    /// Performs the actual search for non-delivery reports.
    /// </summary>
    /// <param name="since">Only reports after this date are considered.</param>
    /// <param name="before">Only reports before this date are considered.</param>
    /// <param name="recipientContains">Filters reports by recipient substring.</param>
    /// <param name="messageId">Filters reports by message identifier.</param>
    /// <param name="maxResults">Maximum number of reports to return.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Collection of parsed non-delivery reports.</returns>
    protected abstract Task<IList<NonDeliveryReport>> SearchInternalAsync(
        DateTime? since,
        DateTime? before,
        string? recipientContains,
        string? messageId,
        int maxResults,
        CancellationToken cancellationToken);
}