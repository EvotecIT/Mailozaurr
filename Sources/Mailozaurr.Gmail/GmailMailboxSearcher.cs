using Mailozaurr.DmarcReports;
using Mailozaurr.NonDeliveryReports;

namespace Mailozaurr;

/// <summary>Searches Gmail mailboxes for structured mail reports.</summary>
public static class GmailMailboxSearcher {
    /// <summary>Searches Gmail for DMARC aggregate reports.</summary>
    /// <param name="client">Authenticated Gmail API client.</param>
    /// <param name="userId">Gmail user identifier, commonly <c>me</c>.</param>
    /// <param name="since">Optional inclusive received-time lower bound.</param>
    /// <param name="before">Optional exclusive received-time upper bound.</param>
    /// <param name="domain">Optional reporting domain filter.</param>
    /// <param name="maxResults">Maximum number of messages to inspect, or zero for the provider default.</param>
    /// <param name="parallelDownloadLimit">Maximum number of MIME messages downloaded concurrently.</param>
    /// <param name="maxUncompressedSize">Maximum uncompressed attachment size to inspect, in bytes.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Parsed DMARC aggregate reports.</returns>
    public static async Task<IList<DmarcReport>> SearchDmarcReportsAsync(
        GmailApiClient client, string userId, DateTime? since = null, DateTime? before = null,
        string? domain = null, int maxResults = 0, int parallelDownloadLimit = 4,
        long maxUncompressedSize = 10 * 1024 * 1024, CancellationToken cancellationToken = default) =>
        await SearchDmarcReportsAsync(
            client,
            userId,
            DmarcReportInspectionOptions.FromLegacyLimit(maxUncompressedSize),
            since,
            before,
            domain,
            maxResults,
            parallelDownloadLimit,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Searches Gmail for DMARC aggregate reports using explicit attachment inspection limits.</summary>
    public static async Task<IList<DmarcReport>> SearchDmarcReportsAsync(
        GmailApiClient client,
        string userId,
        DmarcReportInspectionOptions inspectionOptions,
        DateTime? since = null,
        DateTime? before = null,
        string? domain = null,
        int maxResults = 0,
        int parallelDownloadLimit = 4,
        CancellationToken cancellationToken = default) {
        if (client == null) throw new ArgumentNullException(nameof(client));
        if (inspectionOptions == null) throw new ArgumentNullException(nameof(inspectionOptions));
        var inspectionPolicy = inspectionOptions.CreatePolicy();
        string query = MailboxSearcher.BuildGmailDmarcReportQuery(since, before, domain);
        IList<GmailMessage> messages = await client.ListAsync(userId, query,
            maxResults > 0 ? maxResults : (int?)null, cancellationToken).ConfigureAwait(false);
        string[] messageIds = GetMessageIds(messages);
        IReadOnlyList<MimeMessage> mimeMessages = await DownloadMimeMessagesAsync(
            messageIds, parallelDownloadLimit,
            (id, token) => client.GetMimeMessageAsync(userId, id, token), cancellationToken)
            .ConfigureAwait(false);
        return MailboxSearcher.FilterDmarcReports(
            mimeMessages,
            since,
            before,
            domain,
            inspectionPolicy,
            new SharedReadBudget(inspectionPolicy.MaxTotalUncompressedBytes));
    }

    /// <summary>Searches Gmail for non-delivery reports.</summary>
    public static async Task<IList<NonDeliveryReport>> SearchNonDeliveryReportsAsync(
        GmailApiClient client, string userId, DateTime? since = null, DateTime? before = null,
        string? recipientContains = null, string? messageId = null, int maxResults = 0,
        int parallelDownloadLimit = 4, CancellationToken cancellationToken = default) {
        if (client == null) throw new ArgumentNullException(nameof(client));
        string query = MailboxSearcher.BuildGmailNonDeliveryReportQuery(since, before);
        IList<GmailMessage> messages = await client.ListAsync(userId, query,
            maxResults > 0 ? maxResults : (int?)null, cancellationToken).ConfigureAwait(false);
        string[] messageIds = GetMessageIds(messages);
        IReadOnlyList<MimeMessage> mimeMessages = await DownloadMimeMessagesAsync(
            messageIds, parallelDownloadLimit,
            (id, token) => client.GetMimeMessageAsync(userId, id, token), cancellationToken)
            .ConfigureAwait(false);
        IList<NonDeliveryReport> results = MailboxSearcher.FilterNonDeliveryReports(
            mimeMessages, since, before, recipientContains, messageId);
        return maxResults > 0 ? results.Take(maxResults).ToList() : results;
    }

    internal static Task<IReadOnlyList<MimeMessage>> DownloadMimeMessagesAsync(
        IReadOnlyList<string> messageIds, int parallelDownloadLimit,
        Func<string, CancellationToken, Task<MimeMessage>> download,
        CancellationToken cancellationToken = default) =>
        BoundedMailboxDownloader.DownloadInOrderAsync(
            messageIds, parallelDownloadLimit, download, cancellationToken);

    private static string[] GetMessageIds(IList<GmailMessage> messages) => messages
        .Select(message => message.Id)
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Select(id => id!)
        .ToArray();
}
