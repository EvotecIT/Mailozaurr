using Mailozaurr;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.NonDeliveryReports;

/// <summary>
/// Retrieves non-delivery reports using the Microsoft Graph API.
/// </summary>
public sealed class GraphNonDeliveryReportService : NonDeliveryReportServiceBase {
    private readonly GraphCredential credential;
    private readonly string userPrincipalName;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphNonDeliveryReportService"/> class.
    /// </summary>
    /// <param name="credential">Graph authentication credential.</param>
    /// <param name="userPrincipalName">User principal name to search the mailbox for.</param>
    /// <param name="resolver">Resolver used to match reports with sent messages.</param>
    public GraphNonDeliveryReportService(GraphCredential credential, string userPrincipalName, SendLogResolver resolver) : base(resolver) {
        this.credential = credential;
        this.userPrincipalName = userPrincipalName;
    }

    /// <inheritdoc />
    protected override Task<IList<NonDeliveryReport>> SearchInternalAsync(
        DateTime? since,
        DateTime? before,
        string? recipientContains,
        string? messageId,
        int maxResults,
        CancellationToken cancellationToken) =>
        MailboxSearcher.SearchNonDeliveryReportsAsync(credential, userPrincipalName, since, before, recipientContains, messageId, maxResults, cancellationToken: cancellationToken);
}