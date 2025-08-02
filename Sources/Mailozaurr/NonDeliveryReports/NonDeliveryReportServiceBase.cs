using Mailozaurr;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.NonDeliveryReports;

public abstract class NonDeliveryReportServiceBase : INonDeliveryReportService {
    private readonly SendLogResolver resolver;
    private readonly SemaphoreSlim gate = new(1, 1);

    protected NonDeliveryReportServiceBase(SendLogResolver resolver) => this.resolver = resolver;

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
        }
        finally {
            gate.Release();
        }
        var results = new List<NonDeliveryReportResult>(reports.Count);
        foreach (NonDeliveryReport report in reports) {
            SentMessageRecord? record = await resolver.ResolveAsync(report, cancellationToken).ConfigureAwait(false);
            results.Add(new NonDeliveryReportResult(report, record));
        }
        return results;
    }

    protected abstract Task<IList<NonDeliveryReport>> SearchInternalAsync(
        DateTime? since,
        DateTime? before,
        string? recipientContains,
        string? messageId,
        int maxResults,
        CancellationToken cancellationToken);
}
