using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// High-level delegated-token mailbox browsing helpers built on top of <see cref="GraphApiClient"/>.
/// </summary>
public sealed class GraphMailboxBrowser {
    private const string SummarySelect = "id,subject,receivedDateTime,from,toRecipients,internetMessageId,hasAttachments,isRead,flag,conversationId";
    private readonly GraphApiClient _graph;

    /// <summary>
    /// Maximum MIME payload size used by <see cref="GetMessageContentAsync"/> when no explicit limit is provided.
    /// </summary>
    public const int DefaultMaxMimeBytes = 25 * 1024 * 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphMailboxBrowser"/> class.
    /// </summary>
    /// <param name="graph">Graph API client.</param>
    public GraphMailboxBrowser(GraphApiClient graph) {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
    }

    /// <summary>
    /// Lists messages in a Graph folder.
    /// </summary>
    public async Task<GraphMailboxListResult> ListMessagesAsync(
        string? folder,
        int limit,
        int offset,
        CancellationToken cancellationToken = default) {
        var folderSelector = ResolveFolderSelector(folder);
        var top = ClampInt(limit, 1, 1000);
        var skip = Math.Max(0, offset);

        var folderInfo = await _graph.GetMailFolderAsync(folderSelector, select: "totalItemCount", cancellationToken: cancellationToken).ConfigureAwait(false);
        var totalCount = folderInfo.TotalItemCount ?? 0;

        var page = await _graph.ListMessagesAsync(
            folderSelector,
            top: top,
            skip: skip,
            select: SummarySelect,
            orderBy: "receivedDateTime desc",
            filter: null,
            search: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new GraphMailboxListResult {
            FolderSelector = folderSelector,
            TotalCount = totalCount,
            Messages = MapSummaries(page.Items)
        };
    }

    /// <summary>
    /// Lists messages in a Graph conversation.
    /// </summary>
    public async Task<IReadOnlyList<GraphMailboxMessageSummary>> ListConversationMessagesAsync(
        string conversationId,
        int top = 100,
        int maxPages = 25,
        int maxItems = 2000,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(conversationId)) {
            throw new ArgumentException("conversationId is required.", nameof(conversationId));
        }

        var items = await _graph.ListConversationMessagesAsync(
            conversationId.Trim(),
            top: ClampInt(top, 1, 1000),
            maxPages: ClampInt(maxPages, 1, 100),
            select: SummarySelect,
            orderBy: "receivedDateTime desc",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var output = MapSummaries(items);
        output.Sort(static (a, b) => b.DateUtc.CompareTo(a.DateUtc));
        var cap = ClampInt(maxItems, 1, 10000);
        if (output.Count > cap) {
            output = output.GetRange(0, cap);
        }
        return output;
    }

    /// <summary>
    /// Searches messages in a Graph folder.
    /// </summary>
    public async Task<GraphMailboxSearchResult> SearchMessagesAsync(
        GraphMailboxSearchRequest request,
        int max,
        CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var limit = ClampInt(max, 1, 1000);
        var folderSelector = ResolveFolderSelector(request.Folder);
        var hasText = !string.IsNullOrWhiteSpace(request.Query) ||
                      !string.IsNullOrWhiteSpace(request.SubjectContains) ||
                      !string.IsNullOrWhiteSpace(request.FromContains) ||
                      !string.IsNullOrWhiteSpace(request.ToContains) ||
                      !string.IsNullOrWhiteSpace(request.BodyContains);

        var filterParts = new List<string>();
        if (!hasText) {
            if (request.UnseenOnly) {
                filterParts.Add("isRead eq false");
            }
            if (request.HasAttachment) {
                filterParts.Add("hasAttachments eq true");
            }
            if (request.SinceUtc.HasValue) {
                filterParts.Add("receivedDateTime ge " + request.SinceUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            }
            if (request.BeforeUtc.HasValue) {
                filterParts.Add("receivedDateTime lt " + request.BeforeUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            }
        }

        string? filter = filterParts.Count == 0 ? null : string.Join(" and ", filterParts);
        string? search = null;
        if (hasText) {
            var tokens = new List<string>();
            AddSearchToken(tokens, request.Query);
            AddSearchToken(tokens, request.SubjectContains);
            AddSearchToken(tokens, request.FromContains);
            AddSearchToken(tokens, request.ToContains);
            AddSearchToken(tokens, request.BodyContains);
            search = tokens.Count == 0 ? null : string.Join(" ", tokens);
        }

        var page = await _graph.ListMessagesAsync(
            folderSelector,
            top: limit,
            skip: null,
            select: SummarySelect,
            orderBy: "receivedDateTime desc",
            filter: filter,
            search: search,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var messages = MapSummaries(page.Items);
        messages.Sort(static (a, b) => b.DateUtc.CompareTo(a.DateUtc));
        if (messages.Count > limit) {
            messages = messages.GetRange(0, limit);
        }
        return new GraphMailboxSearchResult {
            FolderSelector = folderSelector,
            Messages = messages
        };
    }

    /// <summary>
    /// Performs Graph mailbox delta query for a folder.
    /// </summary>
    public async Task<GraphMailboxDeltaResult> DeltaMessagesAsync(
        string folder,
        string? cursor,
        int max,
        CancellationToken cancellationToken = default) {
        var folderSelector = ResolveFolderSelector(folder);
        var delta = await _graph.DeltaMessagesAsync(
            folderSelector,
            cursor: cursor,
            top: ClampInt(max, 1, 1000),
            select: SummarySelect,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var deleted = delta.DeletedIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList()
                      ?? new List<string>();
        return new GraphMailboxDeltaResult {
            FolderSelector = folderSelector,
            Cursor = delta.Cursor,
            Upserts = MapSummaries(delta.Items),
            DeletedNativeIds = deleted
        };
    }

    /// <summary>
    /// Gets one message summary.
    /// </summary>
    public async Task<GraphMailboxMessageSummary> GetMessageSummaryAsync(
        string messageId,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        var message = await _graph.GetMessageAsync(
            messageId.Trim(),
            select: SummarySelect,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return MapSummary(message);
    }

    /// <summary>
    /// Gets one message content by downloading MIME payload and parsing it to <see cref="MimeMessage"/>.
    /// </summary>
    public async Task<GraphMailboxGetResult> GetMessageContentAsync(
        string messageId,
        int maxMimeBytes = DefaultMaxMimeBytes,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        if (maxMimeBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxMimeBytes), "maxMimeBytes must be greater than zero.");
        }

        var normalizedId = messageId.Trim();
        var meta = await _graph.GetMessageAsync(normalizedId, select: "id,isRead,flag", cancellationToken: cancellationToken).ConfigureAwait(false);
        var mimeBytes = await _graph.GetMessageMimeAsync(normalizedId, maxBytes: maxMimeBytes, cancellationToken: cancellationToken).ConfigureAwait(false);
        try {
            return new GraphMailboxGetResult {
                Seen = meta.IsRead,
                Flagged = meta.Flag == null ? null : IsFlagged(meta.Flag),
                Message = MimeMessage.Load(new MemoryStream(mimeBytes, writable: false))
            };
        } catch (Exception ex) {
            throw new InvalidDataException("Failed to parse Graph MIME message.", ex);
        }
    }

    /// <summary>
    /// Sets message read/unread state.
    /// </summary>
    public async Task SetMessageSeenAsync(
        string messageId,
        bool seen,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        await _graph.SetMessageIsReadAsync(
            messageId.Trim(),
            seen,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets message flagged/unflagged state.
    /// </summary>
    public async Task SetMessageFlaggedAsync(
        string messageId,
        bool flagged,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        await _graph.SetMessageFlaggedAsync(
            messageId.Trim(),
            flagged,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves a message to a target folder alias/id.
    /// </summary>
    public async Task MoveMessageAsync(
        string messageId,
        string targetFolder,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        var destinationId = await ResolveFolderIdAsync(targetFolder, cancellationToken).ConfigureAwait(false);
        await _graph.MoveMessageAsync(
            messageId.Trim(),
            destinationId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a message.
    /// </summary>
    public async Task DeleteMessageAsync(
        string messageId,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        await _graph.DeleteMessageAsync(
            messageId.Trim(),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves many messages to a target folder alias/id.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> MoveMessagesAsync(
        IEnumerable<string> messageIds,
        string targetFolder,
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        var destinationId = await ResolveFolderIdAsync(targetFolder, cancellationToken).ConfigureAwait(false);
        return await _graph.BatchMoveMessagesAsync(
            messageIds,
            destinationId,
            batchSize: batchSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes many messages.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> DeleteMessagesAsync(
        IEnumerable<string> messageIds,
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        return await _graph.BatchDeleteMessagesAsync(
            messageIds,
            batchSize: batchSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets read/unread state on many messages.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> SetMessagesSeenAsync(
        IEnumerable<string> messageIds,
        bool seen,
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        return await _graph.BatchSetMessagesIsReadAsync(
            messageIds,
            seen,
            batchSize: batchSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets flagged/unflagged state on many messages.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> SetMessagesFlaggedAsync(
        IEnumerable<string> messageIds,
        bool flagged,
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        return await _graph.BatchSetMessagesFlaggedAsync(
            messageIds,
            flagged,
            batchSize: batchSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves many conversations to a target folder alias/id.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> MoveConversationsAsync(
        IEnumerable<string> conversationIds,
        string targetFolder,
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        if (conversationIds == null) {
            throw new ArgumentNullException(nameof(conversationIds));
        }

        var destinationId = await ResolveFolderIdAsync(targetFolder, cancellationToken).ConfigureAwait(false);
        return await _graph.BatchMoveConversationsAsync(
            conversationIds,
            destinationId,
            batchSize: batchSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes many conversations.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> DeleteConversationsAsync(
        IEnumerable<string> conversationIds,
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        if (conversationIds == null) {
            throw new ArgumentNullException(nameof(conversationIds));
        }

        return await _graph.BatchDeleteConversationsAsync(
            conversationIds,
            batchSize: batchSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ResolveFolderIdAsync(string targetFolder, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(targetFolder)) {
            throw new ArgumentException("targetFolder is required.", nameof(targetFolder));
        }

        var folderSelector = ResolveFolderSelector(targetFolder);
        var folder = await _graph.GetMailFolderAsync(
            folderSelector,
            select: "id",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var id = (folder.Id ?? string.Empty).Trim();
        if (id.Length == 0) {
            throw new InvalidOperationException($"Graph folder id resolution returned an empty id for selector '{folderSelector}'.");
        }
        return id;
    }

    private static int ClampInt(int value, int min, int max) {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static void AddSearchToken(List<string> tokens, string? value) {
        if (value != null) {
            var trimmed = value.Trim();
            if (trimmed.Length > 0) {
                tokens.Add(trimmed);
            }
        }
    }

    private static bool IsFlagged(GraphMailMessageFlag? flag) =>
        string.Equals(flag?.FlagStatus, "flagged", StringComparison.OrdinalIgnoreCase);

    private static string JoinRecipients(IReadOnlyList<GraphEmailAddress>? recipients) {
        if (recipients == null || recipients.Count == 0) {
            return string.Empty;
        }
        var items = recipients
            .Select(r => r?.Email?.Address)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();
        return items.Count == 0 ? string.Empty : string.Join(", ", items);
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

    private static GraphMailboxMessageSummary MapSummary(GraphMailMessage message) {
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }

        return new GraphMailboxMessageSummary {
            NativeId = message.Id,
            NativeThreadId = string.IsNullOrWhiteSpace(message.ConversationId) ? null : (message.ConversationId ?? string.Empty).Trim(),
            MessageId = NormalizeMessageIdValue(message.InternetMessageId),
            From = message.From?.Email?.Address ?? string.Empty,
            To = JoinRecipients(message.ToRecipients),
            Subject = message.Subject,
            DateUtc = message.ReceivedDateTime?.UtcDateTime ?? DateTime.UtcNow,
            HasAttachments = message.HasAttachments ?? false,
            Seen = message.IsRead ?? false,
            Flagged = IsFlagged(message.Flag)
        };
    }

    private static List<GraphMailboxMessageSummary> MapSummaries(IReadOnlyList<GraphMailMessage>? messages) {
        if (messages == null || messages.Count == 0) {
            return new List<GraphMailboxMessageSummary>();
        }

        var output = new List<GraphMailboxMessageSummary>(messages.Count);
        foreach (var message in messages) {
            if (message == null) {
                continue;
            }
            output.Add(MapSummary(message));
        }
        return output;
    }

    /// <summary>
    /// Resolves user folder aliases to Graph well-known folder selectors.
    /// </summary>
    public static string ResolveFolderSelector(string? folderRaw) {
        var folder = (folderRaw ?? string.Empty).Trim();
        if (folder.Length == 0) {
            return "inbox";
        }

        if (folder.Equals("INBOX", StringComparison.OrdinalIgnoreCase) ||
            folder.Equals("Inbox", StringComparison.OrdinalIgnoreCase)) {
            return "inbox";
        }

        if (folder.Equals("Sent", StringComparison.OrdinalIgnoreCase) ||
            folder.Equals("Sent Items", StringComparison.OrdinalIgnoreCase) ||
            folder.Equals("SentItems", StringComparison.OrdinalIgnoreCase)) {
            return "sentitems";
        }

        if (folder.Equals("Drafts", StringComparison.OrdinalIgnoreCase)) {
            return "drafts";
        }

        if (folder.Equals("Archive", StringComparison.OrdinalIgnoreCase)) {
            return "archive";
        }

        if (folder.Equals("Junk", StringComparison.OrdinalIgnoreCase) ||
            folder.Equals("Junk Email", StringComparison.OrdinalIgnoreCase) ||
            folder.Equals("Spam", StringComparison.OrdinalIgnoreCase)) {
            return "junkemail";
        }

        if (folder.Equals("Trash", StringComparison.OrdinalIgnoreCase) ||
            folder.Equals("Deleted Items", StringComparison.OrdinalIgnoreCase) ||
            folder.Equals("DeletedItems", StringComparison.OrdinalIgnoreCase)) {
            return "deleteditems";
        }

        // Allow callers to pass a Graph folder id directly.
        return folder;
    }

    /// <summary>
    /// Graph mailbox list result.
    /// </summary>
    public sealed class GraphMailboxListResult {
        /// <summary>Resolved Graph folder selector used for listing.</summary>
        public string FolderSelector { get; set; } = string.Empty;

        /// <summary>Total item count as reported by Graph folder metadata.</summary>
        public int TotalCount { get; set; }

        /// <summary>Message summaries.</summary>
        public List<GraphMailboxMessageSummary> Messages { get; set; } = new();
    }

    /// <summary>
    /// Graph mailbox search request.
    /// </summary>
    public sealed class GraphMailboxSearchRequest {
        /// <summary>Folder filter.</summary>
        public string? Folder { get; set; }

        /// <summary>Free-form Graph search query string.</summary>
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

        /// <summary>Lower bound for received date/time (UTC).</summary>
        public DateTime? SinceUtc { get; set; }

        /// <summary>Upper bound for received date/time (UTC).</summary>
        public DateTime? BeforeUtc { get; set; }
    }

    /// <summary>
    /// Graph mailbox search result.
    /// </summary>
    public sealed class GraphMailboxSearchResult {
        /// <summary>Resolved Graph folder selector used for search.</summary>
        public string FolderSelector { get; set; } = string.Empty;

        /// <summary>Matched messages.</summary>
        public List<GraphMailboxMessageSummary> Messages { get; set; } = new();
    }

    /// <summary>
    /// Graph mailbox delta result.
    /// </summary>
    public sealed class GraphMailboxDeltaResult {
        /// <summary>Resolved Graph folder selector used for delta query.</summary>
        public string FolderSelector { get; set; } = string.Empty;

        /// <summary>Graph delta cursor (next or delta link).</summary>
        public string? Cursor { get; set; }

        /// <summary>Messages to upsert.</summary>
        public List<GraphMailboxMessageSummary> Upserts { get; set; } = new();

        /// <summary>Deleted message ids.</summary>
        public List<string> DeletedNativeIds { get; set; } = new();
    }

    /// <summary>
    /// Graph mailbox get-message result.
    /// </summary>
    public sealed class GraphMailboxGetResult {
        /// <summary>Parsed MIME message.</summary>
        public MimeMessage Message { get; set; } = new MimeMessage();

        /// <summary>Read state from Graph metadata.</summary>
        public bool? Seen { get; set; }

        /// <summary>Flagged state from Graph metadata.</summary>
        public bool? Flagged { get; set; }
    }

    /// <summary>
    /// Provider-agnostic Graph mailbox message summary.
    /// </summary>
    public sealed class GraphMailboxMessageSummary {
        /// <summary>Graph message id.</summary>
        public string NativeId { get; set; } = string.Empty;

        /// <summary>Graph conversation id.</summary>
        public string? NativeThreadId { get; set; }

        /// <summary>Normalized message-id header value.</summary>
        public string? MessageId { get; set; }

        /// <summary>Sender address.</summary>
        public string From { get; set; } = string.Empty;

        /// <summary>Joined recipient list.</summary>
        public string To { get; set; } = string.Empty;

        /// <summary>Subject line.</summary>
        public string? Subject { get; set; }

        /// <summary>Message received date/time (UTC).</summary>
        public DateTime DateUtc { get; set; }

        /// <summary>True when message has attachments.</summary>
        public bool HasAttachments { get; set; }

        /// <summary>True when message is seen.</summary>
        public bool Seen { get; set; }

        /// <summary>True when message is flagged.</summary>
        public bool Flagged { get; set; }
    }
}
