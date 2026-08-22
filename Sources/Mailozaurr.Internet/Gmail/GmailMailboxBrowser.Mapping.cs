using System.IO;

namespace Mailozaurr;

public sealed partial class GmailMailboxBrowser {
    private static int ClampInt(int value, int min, int max) {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static string? NormalizeOptional(string? raw) {
        var trimmed = raw == null ? string.Empty : raw.Trim();
        if (trimmed.Length == 0) {
            return null;
        }
        return trimmed;
    }

    private static DateTime ResolveInternalDateUtc(long? internalDateMs) {
        if (!internalDateMs.HasValue || internalDateMs.Value <= 0) {
            return DateTime.UtcNow;
        }

        try {
            return DateTimeOffset.FromUnixTimeMilliseconds(internalDateMs.Value).UtcDateTime;
        } catch {
            return DateTime.UtcNow;
        }
    }

    private static Dictionary<string, string> BuildHeaderMap(GmailMessagePayload? payload) {
        var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var headers = payload?.Headers;
        if (headers == null || headers.Count == 0) {
            return output;
        }

        foreach (var header in headers) {
            var name = NormalizeOptional(header?.Name);
            if (name == null) {
                continue;
            }

            output[name] = header?.Value ?? string.Empty;
        }

        return output;
    }

    private static bool PayloadHasAttachments(GmailMessagePayload? payload) {
        if (payload == null) {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(payload.Filename) || !string.IsNullOrWhiteSpace(payload.Body?.AttachmentId)) {
            return true;
        }

        var parts = payload.Parts;
        if (parts == null || parts.Count == 0) {
            return false;
        }

        foreach (var part in parts) {
            if (PayloadHasAttachments(part)) {
                return true;
            }
        }

        return false;
    }

    private static bool HasLabel(IReadOnlyCollection<string>? labelIds, string labelId) {
        if (labelIds == null || labelIds.Count == 0 || string.IsNullOrWhiteSpace(labelId)) {
            return false;
        }

        foreach (var current in labelIds) {
            if (current != null && current.Equals(labelId, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private static string? NormalizeMessageIdValue(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        var normalized = value == null ? string.Empty : value.Trim();
        if (normalized.StartsWith("<", StringComparison.Ordinal)) {
            normalized = normalized.Substring(1);
        }
        if (normalized.EndsWith(">", StringComparison.Ordinal)) {
            normalized = normalized.Substring(0, normalized.Length - 1);
        }

        normalized = normalized.Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    private static GmailMailboxMessageSummary MapSummary(GmailMessage message) {
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }

        var headers = BuildHeaderMap(message.Payload);
        _ = headers.TryGetValue("From", out var from);
        _ = headers.TryGetValue("To", out var to);
        _ = headers.TryGetValue("Subject", out var subject);
        _ = headers.TryGetValue("Message-Id", out var messageIdHeader);

        return new GmailMailboxMessageSummary {
            NativeId = NormalizeOptional(message.Id) ?? string.Empty,
            NativeThreadId = NormalizeOptional(message.ThreadId),
            MessageId = NormalizeMessageIdValue(messageIdHeader),
            From = from ?? string.Empty,
            To = to ?? string.Empty,
            Subject = NormalizeOptional(subject),
            DateUtc = ResolveInternalDateUtc(message.InternalDate),
            HasAttachments = PayloadHasAttachments(message.Payload),
            Seen = !HasLabel(message.LabelIds, "UNREAD"),
            Flagged = HasLabel(message.LabelIds, "STARRED")
        };
    }

    private static List<GmailMailboxMessageSummary> MapSummaries(IList<GmailMessage>? messages) {
        if (messages == null || messages.Count == 0) {
            return new List<GmailMailboxMessageSummary>();
        }

        var output = new List<GmailMailboxMessageSummary>(messages.Count);
        foreach (var message in messages) {
            if (message == null) {
                continue;
            }
            output.Add(MapSummary(message));
        }

        return output;
    }

    private static byte[] Base64UrlDecode(string value) {
        var data = value.Replace('-', '+').Replace('_', '/');
        if (data.Length % 4 == 1) {
            throw new InvalidDataException("Attachment data is not a valid Base64 string.");
        }

        var padding = (4 - data.Length % 4) % 4;
        if (padding > 0) {
            data = data.PadRight(data.Length + padding, '=');
        }

        try {
            return Convert.FromBase64String(data);
        } catch (FormatException ex) {
            throw new InvalidDataException("Attachment data is not a valid Base64 string.", ex);
        }
    }

    private static string Base64UrlEncode(byte[] bytes) {
        var base64 = Convert.ToBase64String(bytes);
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static List<string> SplitMessageIdTokens(string? raw) {
        if (string.IsNullOrWhiteSpace(raw)) {
            return new List<string>();
        }

        var output = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tokens = raw!.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens) {
            var normalized = NormalizeMessageIdValue(token);
            if (normalized == null || !seen.Add(normalized)) {
                continue;
            }
            output.Add(normalized);
        }
        return output;
    }

    /// <summary>
    /// Gmail mailbox folder summary.
    /// </summary>
    public sealed class GmailMailboxFolderSummary {
        /// <summary>Gmail label id.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gmail label display name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gmail label type (for example, <c>system</c> or <c>user</c>).</summary>
        public string? Type { get; set; }
    }

    /// <summary>
    /// Gmail mailbox list result.
    /// </summary>
    public sealed class GmailMailboxListResult {
        /// <summary>Resolved Gmail label id used for listing.</summary>
        public string ResolvedLabelId { get; set; } = string.Empty;

        /// <summary>Total item count estimate as reported by Gmail list response.</summary>
        public int TotalCount { get; set; }

        /// <summary>Message summaries.</summary>
        public List<GmailMailboxMessageSummary> Messages { get; set; } = new();
    }

    /// <summary>
    /// Gmail mailbox thread list result.
    /// </summary>
    public sealed class GmailMailboxThreadListResult {
        /// <summary>Gmail thread id used for listing.</summary>
        public string ThreadId { get; set; } = string.Empty;

        /// <summary>Total number of messages available in the thread slice source.</summary>
        public int TotalCount { get; set; }

        /// <summary>Paged thread message summaries.</summary>
        public List<GmailMailboxMessageSummary> Messages { get; set; } = new();
    }

    /// <summary>
    /// Gmail mailbox search request.
    /// </summary>
    public sealed class GmailMailboxSearchRequest {
        /// <summary>Folder/label filter.</summary>
        public string? Folder { get; set; }

        /// <summary>Free-form Gmail query text.</summary>
        public string? Query { get; set; }

        /// <summary>Subject contains filter.</summary>
        public string? SubjectContains { get; set; }

        /// <summary>From contains filter.</summary>
        public string? FromContains { get; set; }

        /// <summary>To contains filter.</summary>
        public string? ToContains { get; set; }

        /// <summary>Body contains filter.</summary>
        public string? BodyContains { get; set; }

        /// <summary>Unseen-only filter.</summary>
        public bool UnseenOnly { get; set; }

        /// <summary>Has-attachment filter.</summary>
        public bool HasAttachment { get; set; }

        /// <summary>Lower bound for message date/time (UTC).</summary>
        public DateTime? SinceUtc { get; set; }

        /// <summary>Upper bound for message date/time (UTC).</summary>
        public DateTime? BeforeUtc { get; set; }
    }

    /// <summary>
    /// Gmail mailbox search result.
    /// </summary>
    public sealed class GmailMailboxSearchResult {
        /// <summary>Resolved Gmail label id used for searching.</summary>
        public string ResolvedLabelId { get; set; } = string.Empty;

        /// <summary>Matched messages.</summary>
        public List<GmailMailboxMessageSummary> Messages { get; set; } = new();
    }

    /// <summary>
    /// Gmail mailbox import result.
    /// </summary>
    public sealed class GmailMailboxImportResult {
        /// <summary>Label id used for import.</summary>
        public string? LabelId { get; set; }

        /// <summary>Imported Gmail native message id, when available.</summary>
        public string? NativeId { get; set; }

        /// <summary>Imported Gmail native thread id, when available.</summary>
        public string? NativeThreadId { get; set; }
    }

    /// <summary>
    /// Gmail mailbox send result.
    /// </summary>
    public sealed class GmailMailboxSendResult {
        /// <summary>Sent Gmail native message id, when available.</summary>
        public string? NativeId { get; set; }

        /// <summary>Sent Gmail native thread id, when available.</summary>
        public string? NativeThreadId { get; set; }
    }

    /// <summary>
    /// Gmail mailbox duplicate probe result.
    /// </summary>
    public sealed class GmailMailboxDuplicateProbeResult {
        /// <summary>True when a matching message was found.</summary>
        public bool IsMatch { get; set; }

        /// <summary>Label id used for probing.</summary>
        public string? LabelId { get; set; }

        /// <summary>Matched Gmail native message id, when available.</summary>
        public string? NativeId { get; set; }

        /// <summary>Matched Gmail native thread id, when available.</summary>
        public string? NativeThreadId { get; set; }

        /// <summary>Matched normalized RFC822 Message-Id.</summary>
        public string? MessageId { get; set; }
    }

    /// <summary>
    /// Gmail mailbox threading metadata.
    /// </summary>
    public sealed class GmailMailboxThreadingMetadataResult {
        /// <summary>Normalized RFC822 Message-Id.</summary>
        public string? MessageId { get; set; }

        /// <summary>Reply-To header value.</summary>
        public string? ReplyTo { get; set; }

        /// <summary>Cc header value.</summary>
        public string? Cc { get; set; }

        /// <summary>Normalized RFC822 In-Reply-To value.</summary>
        public string? InReplyTo { get; set; }

        /// <summary>Normalized RFC822 References tokens.</summary>
        public List<string> References { get; set; } = new();
    }

    /// <summary>
    /// Gmail mailbox get-message result.
    /// </summary>
    public sealed class GmailMailboxGetResult {
        /// <summary>Parsed MIME message.</summary>
        public MimeMessage Message { get; set; } = new MimeMessage();

        /// <summary>Read state from Gmail labels.</summary>
        public bool? Seen { get; set; }

        /// <summary>Flagged state from Gmail labels.</summary>
        public bool? Flagged { get; set; }

        /// <summary>Gmail thread id.</summary>
        public string? NativeThreadId { get; set; }
    }

    /// <summary>
    /// Gmail mailbox profile result.
    /// </summary>
    public sealed class GmailMailboxProfileResult {
        /// <summary>Email address associated with the mailbox.</summary>
        public string? EmailAddress { get; set; }

        /// <summary>Total number of messages.</summary>
        public long MessagesTotal { get; set; }

        /// <summary>Total number of threads.</summary>
        public long ThreadsTotal { get; set; }

        /// <summary>Current mailbox history id.</summary>
        public string? HistoryId { get; set; }
    }

    /// <summary>
    /// Gmail mailbox watch result.
    /// </summary>
    public sealed class GmailMailboxWatchResult {
        /// <summary>History id returned by watch.</summary>
        public string? HistoryId { get; set; }

        /// <summary>Watch expiration in UTC when available.</summary>
        public DateTime? ExpirationUtc { get; set; }

        /// <summary>Resolved label ids used for the watch request.</summary>
        public List<string> LabelIds { get; set; } = new();
    }

    /// <summary>
    /// Gmail mailbox stop-watch result.
    /// </summary>
    public sealed class GmailMailboxStopWatchResult {
        /// <summary>True when stop call succeeded.</summary>
        public bool Stopped { get; set; }

        /// <summary>True when stop succeeded because watch was already missing.</summary>
        public bool AlreadyStopped { get; set; }
    }

    /// <summary>
    /// Gmail mailbox history result.
    /// </summary>
    public sealed class GmailMailboxHistoryResult {
        /// <summary>Resolved Gmail label id used for history query.</summary>
        public string ResolvedLabelId { get; set; } = string.Empty;

        /// <summary>Newest history id seen while reading history pages.</summary>
        public string? NewHistoryId { get; set; }

        /// <summary>Provider token for the next history page, when more pages remain.</summary>
        public string? NextPageToken { get; set; }

        /// <summary>Message ids that should be upserted.</summary>
        public List<string> UpsertNativeIds { get; set; } = new();

        /// <summary>Message ids that should be deleted.</summary>
        public List<string> DeletedNativeIds { get; set; } = new();
    }

    /// <summary>
    /// Gmail mailbox bulk action result.
    /// </summary>
    public sealed class GmailMailboxBulkOperationResult : MailboxBulkOperationResult;

    /// <summary>
    /// Provider-agnostic Gmail mailbox message summary.
    /// </summary>
    public sealed class GmailMailboxMessageSummary {
        /// <summary>Gmail message id.</summary>
        public string NativeId { get; set; } = string.Empty;

        /// <summary>Gmail thread id.</summary>
        public string? NativeThreadId { get; set; }

        /// <summary>Normalized message-id header value.</summary>
        public string? MessageId { get; set; }

        /// <summary>Sender address.</summary>
        public string From { get; set; } = string.Empty;

        /// <summary>Recipient list.</summary>
        public string To { get; set; } = string.Empty;

        /// <summary>Subject line.</summary>
        public string? Subject { get; set; }

        /// <summary>Message date/time (UTC).</summary>
        public DateTime DateUtc { get; set; }

        /// <summary>True when message has attachments.</summary>
        public bool HasAttachments { get; set; }

        /// <summary>True when message is seen.</summary>
        public bool Seen { get; set; }

        /// <summary>True when message is flagged.</summary>
        public bool Flagged { get; set; }
    }
}
