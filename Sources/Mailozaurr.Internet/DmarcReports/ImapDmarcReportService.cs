using MailKit.Net.Imap;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.DmarcReports;

/// <summary>
/// Retrieves DMARC aggregate reports from an IMAP mailbox.
/// </summary>
public sealed class ImapDmarcReportService : DmarcReportServiceBase {
    private readonly ImapClient client;
    private readonly string? folder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImapDmarcReportService"/> class.
    /// </summary>
    /// <param name="client">IMAP client used to access the mailbox.</param>
    /// <param name="folder">Optional folder name to search within.</param>
    public ImapDmarcReportService(ImapClient client, string? folder = null) {
        this.client = client;
        this.folder = folder;
    }

    /// <inheritdoc />
    protected override Task<IList<DmarcReport>> SearchInternalAsync(
        DateTime? since,
        DateTime? before,
        string? domain,
        int maxResults,
        CancellationToken cancellationToken) =>
        MailboxSearcher.SearchDmarcReportsAsync(client, folder, since, before, domain, maxResults, cancellationToken: cancellationToken);
}