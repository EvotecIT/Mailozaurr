using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Search;
using Mailozaurr.DmarcReports;
using Mailozaurr.NonDeliveryReports;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Provides mailbox search helpers for IMAP and POP3.
/// </summary>
public static partial class MailboxSearcher {
    /// <summary>
    /// Searches an IMAP mailbox and returns matching messages.
    /// </summary>
    /// <param name="client">Connected IMAP client.</param>
    /// <param name="folder">Optional folder to search. Defaults to the inbox.</param>
    /// <param name="subject">Optional "subject contains" filter.</param>
    /// <param name="fromContains">Optional "from contains" filter.</param>
    /// <param name="toContains">Optional "to contains" filter.</param>
    /// <param name="bodyContains">Optional "body contains" filter.</param>
    /// <param name="priority">Optional message priority to match.</param>
    /// <param name="since">Optional UTC lower bound for message delivery dates.</param>
    /// <param name="before">Optional UTC upper bound for message delivery dates.</param>
    /// <param name="hasAttachment">If true, only messages with attachments are returned.</param>
    /// <param name="additionalQueries">Additional IMAP search queries to combine.</param>
    /// <param name="maxResults">Maximum number of results to return. Use 0 for unlimited.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <param name="queryString">Optional free-form query string parsed into filters.</param>
    public static async Task<IList<ImapEmailMessage>> SearchImapAsync(
        ImapClient client,
        string? folder = null,
        string? subject = null,
        string? fromContains = null,
        string? toContains = null,
        string? bodyContains = null,
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
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        if (!string.IsNullOrWhiteSpace(subject)) search = search.And(SearchQuery.SubjectContains(subject!));
        if (!string.IsNullOrWhiteSpace(fromContains)) search = search.And(SearchQuery.FromContains(fromContains!));
        if (!string.IsNullOrWhiteSpace(toContains)) search = search.And(SearchQuery.ToContains(toContains!));
        if (!string.IsNullOrWhiteSpace(bodyContains)) search = search.And(SearchQuery.BodyContains(bodyContains!));
        if (sinceUtc.HasValue) search = search.And(SearchQuery.DeliveredAfter(sinceUtc.Value));
        if (beforeUtc.HasValue) search = search.And(SearchQuery.DeliveredBefore(beforeUtc.Value));
        if (additionalQueries != null) {
            foreach (var q in additionalQueries) {
                if (q != null) search = search.And(q);
            }
        }
        if (!string.IsNullOrWhiteSpace(queryString)) {
            try {
                var parsed = ParseQuery(queryString);
                if (!string.IsNullOrWhiteSpace(parsed.Subject)) search = search.And(SearchQuery.SubjectContains(parsed.Subject!));
                if (!string.IsNullOrWhiteSpace(parsed.FromContains)) search = search.And(SearchQuery.FromContains(parsed.FromContains!));
                if (!string.IsNullOrWhiteSpace(parsed.ToContains)) search = search.And(SearchQuery.ToContains(parsed.ToContains!));
                if (!string.IsNullOrWhiteSpace(parsed.BodyContains)) search = search.And(SearchQuery.BodyContains(parsed.BodyContains!));
                var parsedSince = NormalizeToUtc(parsed.Since);
                var parsedBefore = NormalizeToUtc(parsed.Before);
                if (parsedSince.HasValue) search = search.And(SearchQuery.DeliveredAfter(parsedSince.Value));
                if (parsedBefore.HasValue) search = search.And(SearchQuery.DeliveredBefore(parsedBefore.Value));
                foreach (var q in parsed.AdditionalQueries) search = search.And(q);
                hasAttachment |= parsed.HasAttachment;
                if (!priority.HasValue) priority = parsed.Priority;
            } catch (Exception ex) {
                LoggingMessages.Logger.WriteWarning("Failed to parse IMAP query string: {0}", ex.Message);
            }
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
    /// <param name="client">Connected POP3 client.</param>
    /// <param name="subject">Optional "subject contains" filter.</param>
    /// <param name="fromContains">Optional "from contains" filter.</param>
    /// <param name="toContains">Optional "to contains" filter.</param>
    /// <param name="bodyContains">Optional "body contains" filter.</param>
    /// <param name="priority">Optional message priority to match.</param>
    /// <param name="since">Optional UTC lower bound for message delivery dates.</param>
    /// <param name="before">Optional UTC upper bound for message delivery dates.</param>
    /// <param name="hasAttachment">If true, only messages with attachments are returned.</param>
    /// <param name="maxResults">Maximum number of results to return. Use 0 for unlimited.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <param name="queryString">Optional free-form query string parsed into filters.</param>
    public static async Task<IList<Pop3EmailMessage>> SearchPop3Async(
        Pop3Client client,
        string? subject = null,
        string? fromContains = null,
        string? toContains = null,
        string? bodyContains = null,
        MessagePriority? priority = null,
        DateTime? since = null,
        DateTime? before = null,
        bool hasAttachment = false,
        int maxResults = 0,
        CancellationToken cancellationToken = default,
        string? queryString = null) {
        IReadOnlyList<string> messageContainsTerms = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(queryString)) {
            try {
                var parsed = ParseQuery(queryString);
                if (string.IsNullOrWhiteSpace(subject)) subject = parsed.Subject;
                if (string.IsNullOrWhiteSpace(fromContains)) fromContains = parsed.FromContains;
                if (string.IsNullOrWhiteSpace(toContains)) toContains = parsed.ToContains;
                if (string.IsNullOrWhiteSpace(bodyContains)) bodyContains = parsed.BodyContains;
                if (!since.HasValue) since = parsed.Since;
                if (!before.HasValue) before = parsed.Before;
                if (!priority.HasValue) priority = parsed.Priority;
                hasAttachment |= parsed.HasAttachment;
                messageContainsTerms = parsed.MessageContainsTerms;
            } catch (Exception ex) {
                LoggingMessages.Logger.WriteWarning("Failed to parse POP3 query string: {0}", ex.Message);
            }
        }
        var results = new List<Pop3EmailMessage>();
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        for (int i = 0; i < client.Count; i++) {
            var message = await client.GetMessageAsync(i, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(subject) && (message.Subject == null || message.Subject.IndexOf(subject, StringComparison.OrdinalIgnoreCase) < 0)) continue;
            if (!string.IsNullOrWhiteSpace(fromContains) && !AddressMatches(message.From, fromContains!)) continue;
            if (!string.IsNullOrWhiteSpace(toContains) && !AddressMatches(message.To, toContains!)) continue;
            if (!string.IsNullOrWhiteSpace(bodyContains)) {
                var textBody = message.TextBody ?? string.Empty;
                var htmlBody = message.HtmlBody ?? string.Empty;
                if (textBody.IndexOf(bodyContains, StringComparison.OrdinalIgnoreCase) < 0 &&
                    htmlBody.IndexOf(bodyContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
            }
            var msgDate = message.Date.UtcDateTime;
            if (sinceUtc.HasValue && msgDate < sinceUtc.Value) continue;
            if (beforeUtc.HasValue && msgDate > beforeUtc.Value) continue;
            if (priority.HasValue && message.Priority != ConvertPriority(priority.Value)) continue;
            if (hasAttachment && !message.Attachments.Any()) continue;
            if (messageContainsTerms.Any(term => !MessageContains(message, term))) continue;
            results.Add(new Pop3EmailMessage(i, message));
            if (maxResults > 0 && results.Count >= maxResults) break;
        }
        return results;
    }

    private static bool MessageContains(MimeMessage message, string text) {
        if (message.Headers.Any(header =>
                header.Value?.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)) {
            return true;
        }

        foreach (var part in message.BodyParts) {
            if (part.Headers.Any(header =>
                    header.Value?.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)) {
                return true;
            }
            if (part is TextPart textPart &&
                textPart.Text?.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }
        }

        return false;
    }




}
