using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.DmarcReports;

/// <summary>
/// Retrieves DMARC aggregate reports using the Gmail API.
/// </summary>
public sealed class GmailDmarcReportService : DmarcReportServiceBase {
    private readonly GmailApiClient client;
    private readonly string userId;

    /// <summary>
    /// Initializes a new instance of the <see cref="GmailDmarcReportService"/> class.
    /// </summary>
    /// <param name="client">Gmail API client.</param>
    /// <param name="userId">Account identifier, typically 'me'.</param>
    public GmailDmarcReportService(GmailApiClient client, string userId) {
        this.client = client;
        this.userId = userId;
    }

    /// <inheritdoc />
    protected override Task<IList<DmarcReport>> SearchInternalAsync(
        DateTime? since,
        DateTime? before,
        string? domain,
        int maxResults,
        CancellationToken cancellationToken) =>
        GmailMailboxSearcher.SearchDmarcReportsAsync(client, userId, since, before, domain, maxResults,
            cancellationToken: cancellationToken);
}
