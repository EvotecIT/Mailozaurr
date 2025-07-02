using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Search;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
        CancellationToken cancellationToken = default) {
        var mailFolder = client.GetCachedFolder(folder, FolderAccess.ReadOnly);
        SearchQuery query = SearchQuery.All;
        if (!string.IsNullOrWhiteSpace(subject)) query = query.And(SearchQuery.SubjectContains(subject));
        if (!string.IsNullOrWhiteSpace(fromContains)) query = query.And(SearchQuery.FromContains(fromContains));
        if (!string.IsNullOrWhiteSpace(toContains)) query = query.And(SearchQuery.ToContains(toContains));
        if (since.HasValue) query = query.And(SearchQuery.DeliveredAfter(since.Value));
        if (before.HasValue) query = query.And(SearchQuery.DeliveredBefore(before.Value));
        if (additionalQueries != null) {
            foreach (var q in additionalQueries) {
                if (q != null) query = query.And(q);
            }
        }
        var uids = await mailFolder.SearchAsync(query, cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken = default) {
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
}
