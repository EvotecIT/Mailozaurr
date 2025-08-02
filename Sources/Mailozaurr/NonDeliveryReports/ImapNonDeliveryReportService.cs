using Mailozaurr;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Imap;

namespace Mailozaurr.NonDeliveryReports;

public sealed class ImapNonDeliveryReportService : NonDeliveryReportServiceBase {
    private readonly ImapClient client;
    private readonly string? folder;

    public ImapNonDeliveryReportService(ImapClient client, SendLogResolver resolver, string? folder = null) : base(resolver) {
        this.client = client;
        this.folder = folder;
    }

    protected override Task<IList<NonDeliveryReport>> SearchInternalAsync(
        DateTime? since,
        DateTime? before,
        string? recipientContains,
        string? messageId,
        int maxResults,
        CancellationToken cancellationToken) =>
        MailboxSearcher.SearchNonDeliveryReportsAsync(client, folder, since, before, recipientContains, messageId, maxResults, cancellationToken);
}
