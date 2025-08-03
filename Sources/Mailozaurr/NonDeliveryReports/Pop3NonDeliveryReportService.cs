using Mailozaurr;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Pop3;

namespace Mailozaurr.NonDeliveryReports;

public sealed class Pop3NonDeliveryReportService : NonDeliveryReportServiceBase {
    private readonly Pop3Client client;

    public Pop3NonDeliveryReportService(Pop3Client client, SendLogResolver resolver) : base(resolver) => this.client = client;

    protected override Task<IList<NonDeliveryReport>> SearchInternalAsync(
        DateTime? since,
        DateTime? before,
        string? recipientContains,
        string? messageId,
        int maxResults,
        CancellationToken cancellationToken) =>
        MailboxSearcher.SearchNonDeliveryReportsAsync(client, since, before, recipientContains, messageId, maxResults, cancellationToken: cancellationToken);
}
