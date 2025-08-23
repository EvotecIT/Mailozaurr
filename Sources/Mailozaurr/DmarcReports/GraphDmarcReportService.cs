using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.DmarcReports;

/// <summary>
/// Retrieves DMARC aggregate reports using Microsoft Graph.
/// </summary>
public sealed class GraphDmarcReportService : DmarcReportServiceBase {
    private readonly GraphCredential credential;
    private readonly string userPrincipalName;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphDmarcReportService"/> class.
    /// </summary>
    /// <param name="credential">Credential used to access Microsoft Graph.</param>
    /// <param name="userPrincipalName">UPN of the mailbox to query.</param>
    public GraphDmarcReportService(GraphCredential credential, string userPrincipalName) {
        this.credential = credential;
        this.userPrincipalName = userPrincipalName;
    }

    /// <inheritdoc />
    protected override Task<IList<DmarcReport>> SearchInternalAsync(
        DateTime? since,
        DateTime? before,
        string? domain,
        int maxResults,
        CancellationToken cancellationToken) =>
        MailboxSearcher.SearchDmarcReportsAsync(credential, userPrincipalName, since, before, domain, maxResults, cancellationToken: cancellationToken);
}
