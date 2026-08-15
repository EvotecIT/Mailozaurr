using Mailozaurr.DmarcReports;
using Mailozaurr.NonDeliveryReports;

namespace Mailozaurr;

/// <summary>Searches Gmail mailboxes for structured mail reports.</summary>
public static class GmailMailboxSearcher {
    /// <summary>Searches Gmail for DMARC aggregate reports.</summary>
    public static async Task<IList<DmarcReport>> SearchDmarcReportsAsync(
        GmailApiClient client, string userId, DateTime? since = null, DateTime? before = null,
        string? domain = null, int maxResults = 0, int parallelDownloadLimit = 4,
        CancellationToken cancellationToken = default) {
        if (client == null) throw new ArgumentNullException(nameof(client));
        string query = MailboxSearcher.BuildGmailDmarcReportQuery(since, before, domain);
        IList<GmailMessage> messages = await client.ListAsync(userId, query,
            maxResults > 0 ? maxResults : (int?)null, cancellationToken).ConfigureAwait(false);
        string[] messageIds = GetMessageIds(messages);
        IReadOnlyList<MimeMessage> mimeMessages = await DownloadMimeMessagesAsync(
            messageIds, parallelDownloadLimit,
            (id, token) => client.GetMimeMessageAsync(userId, id, token), cancellationToken)
            .ConfigureAwait(false);
        return MailboxSearcher.FilterDmarcReports(mimeMessages, since, before, domain);
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
