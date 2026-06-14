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

public static partial class MailboxSearcher {
    /// <summary>
    /// Filters Non-Delivery Reports by date, recipient and message id.
    /// </summary>
    /// <param name="messages">Collection of MIME messages to inspect.</param>
    /// <param name="since">Optional UTC lower bound for the report timestamp.</param>
    /// <param name="before">Optional UTC upper bound for the report timestamp.</param>
    /// <param name="recipientContains">Optional string that the recipient should contain.</param>
    /// <param name="messageId">Optional original message id to match.</param>
    internal static IList<NonDeliveryReport> FilterNonDeliveryReports(
        IEnumerable<MimeMessage> messages,
        DateTime? since,
        DateTime? before,
        string? recipientContains,
        string? messageId) {
        messageId = NonDeliveryReport.NormalizeMessageId(messageId);
        var results = new List<NonDeliveryReport>();
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        foreach (var message in messages) {
            foreach (var report in MimeKitUtils.GetNonDeliveryReports(message)) {
                var reportDate = (report.LastAttemptDate ?? report.Timestamp).UtcDateTime;
                if (sinceUtc.HasValue && reportDate < sinceUtc.Value) continue;
                if (beforeUtc.HasValue && reportDate > beforeUtc.Value) continue;
                if (!string.IsNullOrWhiteSpace(recipientContains) && !RecipientMatches(report, recipientContains!)) continue;
                if (!string.IsNullOrWhiteSpace(messageId) && !string.Equals(report.OriginalMessageId, messageId, StringComparison.OrdinalIgnoreCase)) continue;
                results.Add(report);
            }
        }
        return results;
    }

    internal static string BuildGmailNonDeliveryReportQuery(DateTime? since, DateTime? before) {
        var sb = new StringBuilder();
        bool first = true;
        foreach (var pattern in NonDeliveryReportSubjectPatterns.Values) {
            if (!first) sb.Append(" OR ");
            sb.Append("subject:\"").Append(EscapeGmailQueryValue(pattern)).Append("\"");
            first = false;
        }
        if (sb.Length > 0) {
            sb.Insert(0, "(");
            sb.Append(')');
        }
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        if (sinceUtc.HasValue) sb.Append(' ').Append("after:").Append(sinceUtc.Value.ToString("yyyy'/'MM'/'dd", CultureInfo.InvariantCulture));
        if (beforeUtc.HasValue) sb.Append(' ').Append("before:").Append(beforeUtc.Value.ToString("yyyy'/'MM'/'dd", CultureInfo.InvariantCulture));
        return sb.ToString().Trim();
    }

    private static bool RecipientMatches(NonDeliveryReport report, string filter) {
        var final = report.FinalRecipientAddress;
        if (final != null &&
            final.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        var original = report.OriginalRecipientAddress;
        if (original != null &&
            original.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
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
            if (!string.IsNullOrWhiteSpace(addr.Address) &&
                addr.Address.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            var displayName = addr.Name;
            if (!string.IsNullOrWhiteSpace(displayName) && displayName!.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    internal static SearchQuery BuildNonDeliveryReportSearchQuery(DateTime? since, DateTime? before) {
        SearchQuery search = SearchQuery.HeaderContains("Content-Type", "delivery-status");
        SearchQuery? subjectQuery = null;
        foreach (var pattern in NonDeliveryReportSubjectPatterns.Values) {
            var q = SearchQuery.SubjectContains(pattern);
            subjectQuery = subjectQuery == null ? q : subjectQuery.Or(q);
        }
        if (subjectQuery != null) search = search.Or(subjectQuery);
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        if (sinceUtc.HasValue) search = search.And(SearchQuery.DeliveredAfter(sinceUtc.Value));
        if (beforeUtc.HasValue) search = search.And(SearchQuery.DeliveredBefore(beforeUtc.Value));
        return search;
    }

    internal sealed class ParsedQuery {
        public string? Subject { get; set; }
        public string? FromContains { get; set; }
        public string? ToContains { get; set; }
        public string? BodyContains { get; set; }
        public MessagePriority? Priority { get; set; }
        public DateTime? Since { get; set; }
        public DateTime? Before { get; set; }
        public bool HasAttachment { get; set; }
        public List<SearchQuery> AdditionalQueries { get; } = new();
    }

    internal static ParsedQuery ParseQuery(string? query) {
        var result = new ParsedQuery();
        if (string.IsNullOrWhiteSpace(query)) return result;
        foreach (var token in SplitTokens(query!)) {
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
                    result.BodyContains = value;
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

    private static DateTime? NormalizeToUtc(DateTime? value) {
        if (!value.HasValue) {
            return null;
        }

        var dt = value.Value;
        if (dt.Kind == DateTimeKind.Unspecified) {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        return dt.ToUniversalTime();
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