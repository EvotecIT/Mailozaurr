using MailKit.Net.Pop3;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.DmarcReports;

/// <summary>
/// Retrieves DMARC aggregate reports using the POP3 protocol.
/// </summary>
public sealed class Pop3DmarcReportService : DmarcReportServiceBase {
    private readonly Pop3Client client;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pop3DmarcReportService"/> class.
    /// </summary>
    /// <param name="client">POP3 client used to access the mailbox.</param>
    public Pop3DmarcReportService(Pop3Client client) => this.client = client;

    /// <inheritdoc />
    protected override Task<IList<DmarcReport>> SearchInternalAsync(
        DateTime? since,
        DateTime? before,
        string? domain,
        int maxResults,
        CancellationToken cancellationToken) =>
        MailboxSearcher.SearchDmarcReportsAsync(client, since, before, domain, maxResults, cancellationToken: cancellationToken);
}