using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Search;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mailozaurr.NonDeliveryReports;

namespace Mailozaurr;

/// <summary>
/// Provides mailbox search helpers for IMAP and POP3.
/// </summary>
public static class MailboxSearcher {
    /// <summary>
    /// Searches an IMAP mailbox and returns matching messages.
    /// </summary>
    public static async Task<IList<ImapEmailMessage>> SearchImapAsync(
        ImapClient client,
        string? folder = null,
        string? subject = null,
        string? fromContains = null,
        string? toContains = null,
        MessagePriority? priority = null,
        DateTime? since = null,
        DateTime? before = null,
        bool hasAttachment = false,
        IEnumerable<SearchQuery>? additionalQueries = null,
        int maxResults = 0,
        CancellationToken cancellationToken = default,
        string? queryString = null) {
        var mailFolder = client.GetCachedFolder(folder, FolderAccess.ReadOnly);
        SearchQuery search = SearchQuery.All;
        if (!string.IsNullOrWhiteSpace(subject)) search = search.And(SearchQuery.SubjectContains(subject));
        if (!string.IsNullOrWhiteSpace(fromContains)) search = search.And(SearchQuery.FromContains(fromContains));
        if (!string.IsNullOrWhiteSpace(toContains)) search = search.And(SearchQuery.ToContains(toContains));
        if (since.HasValue) search = search.And(SearchQuery.DeliveredAfter(since.Value));
        if (before.HasValue) search = search.And(SearchQuery.DeliveredBefore(before.Value));
        if (additionalQueries != null) {
            foreach (var q in additionalQueries) {
                if (q != null) search = search.And(q);
            }
        }
        if (!string.IsNullOrWhiteSpace(queryString)) {
            var parsed = ParseQuery(queryString);
            if (!string.IsNullOrWhiteSpace(parsed.Subject)) search = search.And(SearchQuery.SubjectContains(parsed.Subject));
            if (!string.IsNullOrWhiteSpace(parsed.FromContains)) search = search.And(SearchQuery.FromContains(parsed.FromContains));
            if (!string.IsNullOrWhiteSpace(parsed.ToContains)) search = search.And(SearchQuery.ToContains(parsed.ToContains));
            if (parsed.Since.HasValue) search = search.And(SearchQuery.DeliveredAfter(parsed.Since.Value));
            if (parsed.Before.HasValue) search = search.And(SearchQuery.DeliveredBefore(parsed.Before.Value));
            foreach (var q in parsed.AdditionalQueries) search = search.And(q);
            hasAttachment |= parsed.HasAttachment;
            if (!priority.HasValue) priority = parsed.Priority;
        }
        var uids = await mailFolder.SearchAsync(search, cancellationToken).ConfigureAwait(false);
        var result = new List<ImapEmailMessage>(uids.Count);
        foreach (var uid in uids) {
            var message = await mailFolder.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
            if (hasAttachment && !message.Attachments.Any()) continue;
            if (priority.HasValue && message.Priority != ConvertPriority(priority.Value)) continue;
            result.Add(new ImapEmailMessage(uid, message));
            if (maxResults > 0 && result.Count >= maxResults) break;
        }
        return result;
    }

    /// <summary>
    /// Searches a POP3 mailbox and returns matching messages.
    /// </summary>
    public static async Task<IList<Pop3EmailMessage>> SearchPop3Async(
        Pop3Client client,
        string? subject = null,
        string? fromContains = null,
        string? toContains = null,
        MessagePriority? priority = null,
        DateTime? since = null,
        DateTime? before = null,
        bool hasAttachment = false,
        int maxResults = 0,
        CancellationToken cancellationToken = default,
        string? queryString = null) {
        if (!string.IsNullOrWhiteSpace(queryString)) {
            var parsed = ParseQuery(queryString);
            if (string.IsNullOrWhiteSpace(subject)) subject = parsed.Subject;
            if (string.IsNullOrWhiteSpace(fromContains)) fromContains = parsed.FromContains;
            if (string.IsNullOrWhiteSpace(toContains)) toContains = parsed.ToContains;
            if (!since.HasValue) since = parsed.Since;
            if (!before.HasValue) before = parsed.Before;
            if (!priority.HasValue) priority = parsed.Priority;
            hasAttachment |= parsed.HasAttachment;
        }
        var results = new List<Pop3EmailMessage>();
        for (int i = 0; i < client.Count; i++) {
            var message = await client.GetMessageAsync(i, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(subject) && (message.Subject == null || message.Subject.IndexOf(subject, StringComparison.OrdinalIgnoreCase) < 0)) continue;
            if (!string.IsNullOrWhiteSpace(fromContains) && !AddressMatches(message.From, fromContains)) continue;
            if (!string.IsNullOrWhiteSpace(toContains) && !AddressMatches(message.To, toContains)) continue;
            var msgDate = message.Date.DateTime;
            if (since.HasValue && msgDate < since.Value) continue;
            if (before.HasValue && msgDate > before.Value) continue;
            if (priority.HasValue && message.Priority != ConvertPriority(priority.Value)) continue;
            if (hasAttachment && !message.Attachments.Any()) continue;
            results.Add(new Pop3EmailMessage(i, message));
            if (maxResults > 0 && results.Count >= maxResults) break;
        }
        return results;
    }

    /// <summary>
    /// Searches for Non-Delivery Reports in an IMAP mailbox.
    /// </summary>
    public static async Task<IList<NonDeliveryReport>> SearchNonDeliveryReportsAsync(
        ImapClient client,
        string? folder = null,
        DateTime? since = null,
        DateTime? before = null,
        string? recipientContains = null,
        string? messageId = null,
        int maxResults = 0,
        int parallelDownloadLimit = 4,
        CancellationToken cancellationToken = default) {
        var mailFolder = client.GetCachedFolder(folder, FolderAccess.ReadOnly);
        var search = BuildNonDeliveryReportSearchQuery(since, before);
        var uids = await mailFolder.SearchAsync(search, cancellationToken).ConfigureAwait(false);
        var messages = new List<MimeMessage>(uids.Count);
        if (parallelDownloadLimit <= 1) {
            foreach (var uid in uids) {
                cancellationToken.ThrowIfCancellationRequested();
                var msg = await mailFolder.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
                messages.Add(msg);
                if (maxResults > 0 && messages.Count >= maxResults) break;
            }
        } else {
            using var semaphore = new SemaphoreSlim(parallelDownloadLimit);
            var tasks = new List<Task>();
            foreach (var uid in uids) {
                cancellationToken.ThrowIfCancellationRequested();
                tasks.Add(Task.Run(async () => {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try {
                        cancellationToken.ThrowIfCancellationRequested();
                        var msg = await mailFolder.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
                        lock (messages) messages.Add(msg);
                    } finally {
                        semaphore.Release();
                    }
                }, cancellationToken));
                if (maxResults > 0 && tasks.Count >= maxResults) break;
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        return FilterNonDeliveryReports(messages, since, before, recipientContains, messageId);
    }

    /// <summary>
    /// Searches for Non-Delivery Reports in a POP3 mailbox.
    /// </summary>
    public static async Task<IList<NonDeliveryReport>> SearchNonDeliveryReportsAsync(
        Pop3Client client,
        DateTime? since = null,
        DateTime? before = null,
        string? recipientContains = null,
        string? messageId = null,
        int maxResults = 0,
        int parallelDownloadLimit = 4,
        CancellationToken cancellationToken = default) {
        var count = client.Count;
        var indices = new List<int>(count);
        for (int i = 0; i < count; i++) indices.Add(i);
        var messages = new List<MimeMessage>(indices.Count);
        if (parallelDownloadLimit <= 1) {
            foreach (var idx in indices) {
                cancellationToken.ThrowIfCancellationRequested();
                var msg = await client.GetMessageAsync(idx, cancellationToken).ConfigureAwait(false);
                messages.Add(msg);
                if (maxResults > 0 && messages.Count >= maxResults) break;
            }
        } else {
            using var semaphore = new SemaphoreSlim(parallelDownloadLimit);
            var tasks = new List<Task>();
            foreach (var idx in indices) {
                cancellationToken.ThrowIfCancellationRequested();
                tasks.Add(Task.Run(async () => {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try {
                        cancellationToken.ThrowIfCancellationRequested();
                        var msg = await client.GetMessageAsync(idx, cancellationToken).ConfigureAwait(false);
                        lock (messages) messages.Add(msg);
                    } finally {
                        semaphore.Release();
                    }
                }, cancellationToken));
                if (maxResults > 0 && tasks.Count >= maxResults) break;
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        return FilterNonDeliveryReports(messages, since, before, recipientContains, messageId);
    }

    /// <summary>
    /// Searches for Non-Delivery Reports using Microsoft Graph.
    /// </summary>
    public static async Task<IList<NonDeliveryReport>> SearchNonDeliveryReportsAsync(
        GraphCredential credential,
        string userPrincipalName,
        DateTime? since = null,
        DateTime? before = null,
        string? recipientContains = null,
        string? messageId = null,
        int maxResults = 0,
        int parallelDownloadLimit = 4,
        CancellationToken cancellationToken = default) {
        var filters = new List<string>();
        if (since.HasValue) filters.Add($"receivedDateTime ge {since.Value:o}");
        if (before.HasValue) filters.Add($"receivedDateTime le {before.Value:o}");
        var filter = filters.Count > 0 ? string.Join(" and ", filters) : null;
        var msgs = await MicrosoftGraphUtils.GetMailMessagesAsync(
            credential,
            userPrincipalName,
            new[] { "id" },
            filter,
            maxResults > 0 ? maxResults : (int?)null).ConfigureAwait(false);
        var mimeMessages = new List<MimeMessage>(msgs.Count);
        if (parallelDownloadLimit <= 1) {
            foreach (var m in msgs) {
                cancellationToken.ThrowIfCancellationRequested();
                if (m.TryGetValue("id", out var idObj) && idObj is string id) {
                    var mime = await MicrosoftGraphUtils.GetMailMessageMimeAsync(credential, userPrincipalName, id).ConfigureAwait(false);
                    mimeMessages.Add(mime);
                }
            }
        } else {
            using var semaphore = new SemaphoreSlim(parallelDownloadLimit);
            var tasks = new List<Task>();
            foreach (var m in msgs) {
                cancellationToken.ThrowIfCancellationRequested();
                if (m.TryGetValue("id", out var idObj) && idObj is string id) {
                    tasks.Add(Task.Run(async () => {
                        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                        try {
                            cancellationToken.ThrowIfCancellationRequested();
                            var mime = await MicrosoftGraphUtils.GetMailMessageMimeAsync(credential, userPrincipalName, id).ConfigureAwait(false);
                            lock (mimeMessages) mimeMessages.Add(mime);
                        } finally {
                            semaphore.Release();
                        }
                    }, cancellationToken));
                }
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        return FilterNonDeliveryReports(mimeMessages, since, before, recipientContains, messageId);
    }

    internal static IList<NonDeliveryReport> FilterNonDeliveryReports(
        IEnumerable<MimeMessage> messages,
        DateTime? since,
        DateTime? before,
        string? recipientContains,
        string? messageId) {
        var results = new List<NonDeliveryReport>();
        foreach (var message in messages) {
            foreach (var report in MimeKitUtils.GetNonDeliveryReports(message)) {
                if (since.HasValue && report.Timestamp.DateTime < since.Value) continue;
                if (before.HasValue && report.Timestamp.DateTime > before.Value) continue;
                if (!string.IsNullOrWhiteSpace(recipientContains) && !RecipientMatches(report, recipientContains)) continue;
                if (!string.IsNullOrWhiteSpace(messageId) && !string.Equals(report.OriginalMessageId, messageId, StringComparison.OrdinalIgnoreCase)) continue;
                results.Add(report);
            }
        }
        return results;
    }

    private static bool RecipientMatches(NonDeliveryReport report, string filter) {
        if (!string.IsNullOrWhiteSpace(report.FinalRecipientAddress) &&
            report.FinalRecipientAddress.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (!string.IsNullOrWhiteSpace(report.OriginalRecipientAddress) &&
            report.OriginalRecipientAddress.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private static MimeKit.MessagePriority ConvertPriority(MessagePriority priority)
        => priority switch {
            MessagePriority.High => MimeKit.MessagePriority.Urgent,
            MessagePriority.Low => MimeKit.MessagePriority.NonUrgent,
            _ => MimeKit.MessagePriority.Normal,
        };

    private static bool AddressMatches(InternetAddressList list, string filter) {
        foreach (var addr in list.Mailboxes) {
            if (addr.Address.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (!string.IsNullOrWhiteSpace(addr.Name) && addr.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    private static readonly string[] NdrSubjectPatterns = new[] {
        "Undelivered Mail Returned to Sender",
        "Delivery Status Notification",
        "Mail delivery failed",
        "Mail Delivery Subsystem",
        "Failure Notice"
    };

    internal static SearchQuery BuildNonDeliveryReportSearchQuery(DateTime? since, DateTime? before) {
        SearchQuery search = SearchQuery.HeaderContains("Content-Type", "delivery-status");
        SearchQuery? subjectQuery = null;
        foreach (var pattern in NdrSubjectPatterns) {
            var q = SearchQuery.SubjectContains(pattern);
            subjectQuery = subjectQuery == null ? q : subjectQuery.Or(q);
        }
        if (subjectQuery != null) search = search.Or(subjectQuery);
        if (since.HasValue) search = search.And(SearchQuery.DeliveredAfter(since.Value));
        if (before.HasValue) search = search.And(SearchQuery.DeliveredBefore(before.Value));
        return search;
    }

    internal sealed class ParsedQuery {
        public string? Subject { get; set; }
        public string? FromContains { get; set; }
        public string? ToContains { get; set; }
        public MessagePriority? Priority { get; set; }
        public DateTime? Since { get; set; }
        public DateTime? Before { get; set; }
        public bool HasAttachment { get; set; }
        public List<SearchQuery> AdditionalQueries { get; } = new();
    }

    internal static ParsedQuery ParseQuery(string query) {
        var result = new ParsedQuery();
        if (string.IsNullOrWhiteSpace(query)) return result;
        foreach (var token in SplitTokens(query)) {
            var parts = token.Split(new[] { ':' }, 2);
            if (parts.Length == 2) {
                var key = parts[0];
                var value = Unquote(parts[1]);
                if (string.Equals(key, "from", StringComparison.OrdinalIgnoreCase)) {
                    result.FromContains = value;
                } else if (string.Equals(key, "to", StringComparison.OrdinalIgnoreCase)) {
                    result.ToContains = value;
                } else if (string.Equals(key, "subject", StringComparison.OrdinalIgnoreCase)) {
                    result.Subject = value;
                } else if (string.Equals(key, "since", StringComparison.OrdinalIgnoreCase)) {
                    if (DateTime.TryParse(value, out var sd)) result.Since = sd;
                } else if (string.Equals(key, "before", StringComparison.OrdinalIgnoreCase)) {
                    if (DateTime.TryParse(value, out var bd)) result.Before = bd;
                } else if (string.Equals(key, "priority", StringComparison.OrdinalIgnoreCase)) {
                    if (Enum.TryParse(value, true, out MessagePriority pr)) result.Priority = pr;
                } else if (string.Equals(key, "has", StringComparison.OrdinalIgnoreCase)) {
                    if (value.Equals("attachment", StringComparison.OrdinalIgnoreCase) || value.Equals("attachments", StringComparison.OrdinalIgnoreCase)) result.HasAttachment = true;
                } else if (string.Equals(key, "body", StringComparison.OrdinalIgnoreCase)) {
                    result.AdditionalQueries.Add(SearchQuery.BodyContains(value));
                } else {
                    result.AdditionalQueries.Add(SearchQuery.MessageContains(token));
                }
            } else {
                var text = Unquote(token);
                result.AdditionalQueries.Add(SearchQuery.MessageContains(text));
            }
        }
        return result;
    }

    private static IEnumerable<string> SplitTokens(string input) {
        var list = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        foreach (var ch in input) {
            if (ch == '"') {
                inQuotes = !inQuotes;
            } else if (char.IsWhiteSpace(ch) && !inQuotes) {
                if (current.Length > 0) {
                    list.Add(current.ToString());
                    current.Clear();
                }
            } else {
                current.Append(ch);
            }
        }
        if (current.Length > 0) list.Add(current.ToString());
        return list;
    }

    private static string Unquote(string value) {
        if (value.Length > 1 &&
            value.StartsWith("\"", StringComparison.Ordinal) &&
            value.EndsWith("\"", StringComparison.Ordinal))
            return value.Substring(1, value.Length - 2);
        return value;
    }
}
