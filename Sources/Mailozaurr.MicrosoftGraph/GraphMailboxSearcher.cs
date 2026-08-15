using Mailozaurr.DmarcReports;
using Mailozaurr.NonDeliveryReports;

namespace Mailozaurr;

/// <summary>Searches Microsoft Graph mailboxes for structured mail reports.</summary>
public static class GraphMailboxSearcher {
    /// <summary>Searches a Graph mailbox for DMARC aggregate reports.</summary>
    /// <param name="maxUncompressedSize">Maximum uncompressed attachment size to inspect, in bytes.</param>
    public static async Task<IList<DmarcReport>> SearchDmarcReportsAsync(
        GraphCredential credential, string userPrincipalName, DateTime? since = null,
        DateTime? before = null, string? domain = null, int maxResults = 0,
        int parallelDownloadLimit = 4, long maxUncompressedSize = 10 * 1024 * 1024,
        CancellationToken cancellationToken = default) {
        var filters = new List<string> { "hasAttachments eq true", "contains(subject,'report domain')" };
        DateTime? sinceUtc = NormalizeToUtc(since);
        DateTime? beforeUtc = NormalizeToUtc(before);
        if (sinceUtc.HasValue) filters.Add($"receivedDateTime ge {sinceUtc.Value:o}");
        if (beforeUtc.HasValue) filters.Add($"receivedDateTime le {beforeUtc.Value:o}");
        if (!string.IsNullOrWhiteSpace(domain)) filters.Add($"contains(subject,'{domain!.Replace("'", "''")}')");
        IList<Dictionary<string, object>> messages = await MicrosoftGraphUtils.GetMailMessagesAsync(
            credential, userPrincipalName, new[] { "id" }, string.Join(" and ", filters),
            maxResults > 0 ? maxResults : (int?)null, cancellationToken).ConfigureAwait(false);
        string[] messageIds = GetMessageIds(messages);
        IReadOnlyList<MimeMessage> mimeMessages = await DownloadMimeMessagesAsync(
            messageIds, parallelDownloadLimit,
            (id, token) => MicrosoftGraphUtils.GetMailMessageMimeAsync(
                credential, userPrincipalName, id, token), cancellationToken).ConfigureAwait(false);
        return MailboxSearcher.FilterDmarcReports(mimeMessages, since, before, domain, maxUncompressedSize);
    }

    /// <summary>Searches a Graph mailbox for non-delivery reports.</summary>
    public static async Task<IList<NonDeliveryReport>> SearchNonDeliveryReportsAsync(
        GraphCredential credential, string userPrincipalName, DateTime? since = null,
        DateTime? before = null, string? recipientContains = null, string? messageId = null,
        int maxResults = 0, int parallelDownloadLimit = 4,
        CancellationToken cancellationToken = default) {
        var filters = new List<string>();
        DateTime? sinceUtc = NormalizeToUtc(since);
        DateTime? beforeUtc = NormalizeToUtc(before);
        var subjects = NonDeliveryReportSubjectPatterns.Values
            .Select(pattern => $"contains(subject,'{pattern.Replace("'", "''")}')").ToArray();
        if (subjects.Length > 0) filters.Add($"({string.Join(" or ", subjects)})");
        if (sinceUtc.HasValue) filters.Add($"receivedDateTime ge {sinceUtc.Value:o}");
        if (beforeUtc.HasValue) filters.Add($"receivedDateTime le {beforeUtc.Value:o}");
        IList<Dictionary<string, object>> messages = await MicrosoftGraphUtils.GetMailMessagesAsync(
            credential, userPrincipalName, new[] { "id" },
            filters.Count == 0 ? null : string.Join(" and ", filters),
            maxResults > 0 ? maxResults : (int?)null, cancellationToken).ConfigureAwait(false);
        string[] messageIds = GetMessageIds(messages);
        IReadOnlyList<MimeMessage> mimeMessages = await DownloadMimeMessagesAsync(
            messageIds, parallelDownloadLimit,
            (id, token) => MicrosoftGraphUtils.GetMailMessageMimeAsync(
                credential, userPrincipalName, id, token), cancellationToken).ConfigureAwait(false);
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

    private static string[] GetMessageIds(IList<Dictionary<string, object>> messages) => messages
        .Select(message => message.TryGetValue("id", out object? value) ? value as string : null)
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Select(id => id!)
        .ToArray();

    private static DateTime? NormalizeToUtc(DateTime? value) => value.HasValue
        ? (value.Value.Kind == DateTimeKind.Utc ? value.Value : value.Value.ToUniversalTime())
        : null;
}
