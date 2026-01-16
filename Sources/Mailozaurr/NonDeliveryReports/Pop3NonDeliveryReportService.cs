using Mailozaurr;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Pop3;

namespace Mailozaurr.NonDeliveryReports;

/// <summary>
/// Retrieves non-delivery reports using the POP3 protocol.
/// </summary>
public sealed class Pop3NonDeliveryReportService : NonDeliveryReportServiceBase {
    private readonly Pop3Client client;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pop3NonDeliveryReportService"/> class.
    /// </summary>
    /// <param name="client">POP3 client used to access the mailbox.</param>
    /// <param name="resolver">Resolver used to match reports with sent messages.</param>
    public Pop3NonDeliveryReportService(Pop3Client client, SendLogResolver resolver) : base(resolver) => this.client = client;

    /// <inheritdoc />
    protected override Task<IList<NonDeliveryReport>> SearchInternalAsync(
        DateTime? since,
        DateTime? before,
        string? recipientContains,
        string? messageId,
        int maxResults,
        CancellationToken cancellationToken) =>
        MailboxSearcher.SearchNonDeliveryReportsAsync(client, since, before, recipientContains, messageId, maxResults, cancellationToken: cancellationToken);
}
