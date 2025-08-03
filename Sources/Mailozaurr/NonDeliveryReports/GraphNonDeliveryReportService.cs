using Mailozaurr;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.NonDeliveryReports;

public sealed class GraphNonDeliveryReportService : NonDeliveryReportServiceBase {
    private readonly GraphCredential credential;
    private readonly string userPrincipalName;

    public GraphNonDeliveryReportService(GraphCredential credential, string userPrincipalName, SendLogResolver resolver) : base(resolver) {
        this.credential = credential;
        this.userPrincipalName = userPrincipalName;
    }

    protected override Task<IList<NonDeliveryReport>> SearchInternalAsync(
        DateTime? since,
        DateTime? before,
        string? recipientContains,
        string? messageId,
        int maxResults,
        CancellationToken cancellationToken) =>
        MailboxSearcher.SearchNonDeliveryReportsAsync(credential, userPrincipalName, since, before, recipientContains, messageId, maxResults, cancellationToken: cancellationToken);
}
