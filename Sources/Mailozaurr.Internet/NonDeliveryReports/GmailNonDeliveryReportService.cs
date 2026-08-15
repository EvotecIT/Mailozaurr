using Mailozaurr;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.NonDeliveryReports;

/// <summary>
/// Retrieves non-delivery reports using the Gmail API.
/// </summary>
public sealed class GmailNonDeliveryReportService : NonDeliveryReportServiceBase {
    private readonly GmailApiClient client;
    private readonly string userId;

    /// <summary>
    /// Initializes a new instance of the <see cref="GmailNonDeliveryReportService"/> class.
    /// </summary>
    /// <param name="client">Gmail API client used to access the mailbox.</param>
    /// <param name="userId">Gmail account identifier.</param>
    /// <param name="resolver">Resolver used to match reports with sent messages.</param>
    public GmailNonDeliveryReportService(GmailApiClient client, string userId, SendLogResolver resolver) : base(resolver) {
        this.client = client;
        this.userId = userId;
    }

    /// <inheritdoc />
    protected override Task<IList<NonDeliveryReport>> SearchInternalAsync(
        DateTime? since,
        DateTime? before,
        string? recipientContains,
        string? messageId,
        int maxResults,
        CancellationToken cancellationToken) =>
        GmailMailboxSearcher.SearchNonDeliveryReportsAsync(client, userId, since, before, recipientContains,
            messageId, maxResults, cancellationToken: cancellationToken);
}
