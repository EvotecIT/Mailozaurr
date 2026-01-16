using Mailozaurr;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Imap;

namespace Mailozaurr.NonDeliveryReports;

/// <summary>
/// Retrieves non-delivery reports from an IMAP mailbox.
/// </summary>
public sealed class ImapNonDeliveryReportService : NonDeliveryReportServiceBase {
    private readonly ImapClient client;
    private readonly string? folder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImapNonDeliveryReportService"/> class.
    /// </summary>
    /// <param name="client">IMAP client used to access the mailbox.</param>
    /// <param name="resolver">Resolver used to match reports with sent messages.</param>
    /// <param name="folder">Optional folder name to search within.</param>
    public ImapNonDeliveryReportService(ImapClient client, SendLogResolver resolver, string? folder = null) : base(resolver) {
        this.client = client;
        this.folder = folder;
    }

    /// <inheritdoc />
    protected override Task<IList<NonDeliveryReport>> SearchInternalAsync(
        DateTime? since,
        DateTime? before,
        string? recipientContains,
        string? messageId,
        int maxResults,
        CancellationToken cancellationToken) =>
        MailboxSearcher.SearchNonDeliveryReportsAsync(client, folder, since, before, recipientContains, messageId, maxResults, cancellationToken: cancellationToken);
}
