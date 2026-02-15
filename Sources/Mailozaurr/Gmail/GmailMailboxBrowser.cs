using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MimeKit;

namespace Mailozaurr;

/// <summary>
/// High-level delegated-token mailbox browsing helpers built on top of <see cref="GmailApiClient"/>.
/// </summary>
public sealed class GmailMailboxBrowser {
    private const int BatchMaxIds = 1000;
    private const string ListFields = "messages(id,threadId),nextPageToken,resultSizeEstimate";
    private const string MessageSummaryFields = "id,threadId,internalDate,labelIds,payload(headers,name,value,parts,filename,body/attachmentId,body/size,mimeType)";
    private const string ThreadFields = "id,messages(id,threadId,internalDate,labelIds,payload(headers,name,value,parts,filename,body/attachmentId,body/size,mimeType))";
    private const string RawFields = "id,internalDate,labelIds,raw";
    private readonly GmailApiClient _gmail;
    private readonly string _userId;

    /// <summary>
    /// Maximum MIME payload size used by <see cref="GetMessageContentAsync"/> when no explicit limit is provided.
    /// </summary>
    public const int DefaultMaxMimeBytes = 25 * 1024 * 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="GmailMailboxBrowser"/> class.
    /// </summary>
    /// <param name="gmail">Gmail API client.</param>
    /// <param name="userId">Mailbox user id (use <c>me</c> for delegated tokens).</param>
    public GmailMailboxBrowser(GmailApiClient gmail, string userId = "me") {
        _gmail = gmail ?? throw new ArgumentNullException(nameof(gmail));
        _userId = string.IsNullOrWhiteSpace(userId) ? "me" : userId.Trim();
    }

    /// <summary>
    /// Resolves a folder/label selector to a Gmail label id.
    /// </summary>
    public async Task<string?> ResolveLabelIdAsync(
        string? folder,
        CancellationToken cancellationToken = default) {
        var raw = (folder ?? string.Empty).Trim();
        if (raw.Length == 0) {
            return "INBOX";
        }

        if (TryResolveSystemLabel(raw, out var systemLabelId)) {
            return systemLabelId;
        }

        var labels = await _gmail.ListLabelsAsync(_userId, cancellationToken).ConfigureAwait(false);
        foreach (var label in labels) {
            if (label == null) {
                continue;
            }

            var id = NormalizeOptional(label.Id);
            var name = NormalizeOptional(label.Name);
            if (id == null || name == null) {
                continue;
            }
            if (id.Equals(raw, StringComparison.OrdinalIgnoreCase) || name.Equals(raw, StringComparison.OrdinalIgnoreCase)) {
                return id;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a folder selector for Gmail watch requests.
    /// </summary>
    public async Task<string?> ResolveWatchLabelIdAsync(
        string folder,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(folder)) {
            return null;
        }

        if (TryResolveWatchSystemLabel(folder, out var watchLabelId)) {
            return watchLabelId;
        }

        return await ResolveLabelIdAsync(folder, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists available Gmail labels as mailbox folders.
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxFolderSummary>> ListFoldersAsync(
        CancellationToken cancellationToken = default) {
        var labels = await _gmail.ListLabelsAsync(_userId, cancellationToken).ConfigureAwait(false);
        if (labels == null || labels.Count == 0) {
            return Array.Empty<GmailMailboxFolderSummary>();
        }

        var output = new List<GmailMailboxFolderSummary>(labels.Count);
        foreach (var label in labels) {
            if (label == null) {
                continue;
            }

            var id = NormalizeOptional(label.Id);
            var name = NormalizeOptional(label.Name);
            if (id == null || name == null) {
                continue;
            }

            output.Add(new GmailMailboxFolderSummary {
                Id = id,
                Name = name,
                Type = NormalizeOptional(label.Type)
            });
        }

        output.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return output;
    }

    /// <summary>
    /// Lists messages in a Gmail label.
    /// </summary>
    public async Task<GmailMailboxListResult> ListMessagesAsync(
        string? folder,
        int limit,
        int offset,
        CancellationToken cancellationToken = default) {
        var safeLimit = ClampInt(limit, 1, 2000);
        var skipRemaining = Math.Max(0, offset);

        var resolvedLabelId = NormalizeOptional(await ResolveLabelIdAsync(folder, cancellationToken).ConfigureAwait(false));
        if (resolvedLabelId == null) {
            throw new InvalidOperationException("Unable to resolve Gmail folder/label.");
        }

        string? pageToken = null;
        long totalEstimate = 0;
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var selectedIds = new List<string>();

        while (selectedIds.Count < safeLimit) {
            var page = await _gmail.ListPageAsync(
                _userId,
                query: null,
                labelIds: new[] { resolvedLabelId },
                includeSpamTrash: false,
                maxResults: 100,
                pageToken: pageToken,
                fields: ListFields,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (page.ResultSizeEstimate.HasValue && page.ResultSizeEstimate.Value > 0) {
                totalEstimate = page.ResultSizeEstimate.Value;
            }

            pageToken = page.NextPageToken;
            if (page.Messages == null || page.Messages.Count == 0) {
                break;
            }

            foreach (var message in page.Messages) {
                var id = NormalizeOptional(message?.Id);
                if (id == null || !seenIds.Add(id)) {
                    continue;
                }

                if (skipRemaining > 0) {
                    skipRemaining--;
                    continue;
                }

                selectedIds.Add(id);
                if (selectedIds.Count >= safeLimit) {
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(pageToken)) {
                break;
            }
        }

        var summaries = new List<GmailMailboxMessageSummary>(selectedIds.Count);
        foreach (var id in selectedIds) {
            var summary = await TryGetMessageSummaryAsync(id, cancellationToken).ConfigureAwait(false);
            if (summary != null) {
                summaries.Add(summary);
            }
        }

        return new GmailMailboxListResult {
            ResolvedLabelId = resolvedLabelId,
            TotalCount = totalEstimate > int.MaxValue ? int.MaxValue : (int)totalEstimate,
            Messages = summaries
        };
    }

    /// <summary>
    /// Lists messages in a Gmail thread.
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxMessageSummary>> ListThreadMessagesAsync(
        string threadId,
        int maxItems = 2000,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(threadId)) {
            throw new ArgumentException("threadId is required.", nameof(threadId));
        }

        var thread = await _gmail.GetThreadWithOptionsAsync(
            _userId,
            threadId.Trim(),
            format: "full",
            fields: ThreadFields,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var summaries = MapSummaries(thread.Messages);
        summaries.Sort(static (a, b) => b.DateUtc.CompareTo(a.DateUtc));
        var cap = ClampInt(maxItems, 1, 10000);
        if (summaries.Count > cap) {
            summaries = summaries.GetRange(0, cap);
        }

        return summaries;
    }

    /// <summary>
    /// Searches messages in a Gmail label.
    /// </summary>
    public async Task<GmailMailboxSearchResult> SearchMessagesAsync(
        GmailMailboxSearchRequest request,
        int max,
        CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var safeMax = ClampInt(max, 1, 2000);
        var resolvedLabelId = NormalizeOptional(await ResolveLabelIdAsync(request.Folder, cancellationToken).ConfigureAwait(false));
        if (resolvedLabelId == null) {
            throw new InvalidOperationException("Unable to resolve Gmail folder/label.");
        }

        var query = BuildSearchQuery(request);
        var selected = new List<string>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        string? pageToken = null;

        while (selected.Count < safeMax) {
            var page = await _gmail.ListPageAsync(
                _userId,
                query: string.IsNullOrWhiteSpace(query) ? null : query,
                labelIds: new[] { resolvedLabelId },
                includeSpamTrash: false,
                maxResults: 100,
                pageToken: pageToken,
                fields: ListFields,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            pageToken = page.NextPageToken;
            if (page.Messages == null || page.Messages.Count == 0) {
                break;
            }

            foreach (var message in page.Messages) {
                var id = NormalizeOptional(message?.Id);
                if (id == null || !seenIds.Add(id)) {
                    continue;
                }

                selected.Add(id);
                if (selected.Count >= safeMax) {
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(pageToken)) {
                break;
            }
        }

        var summaries = new List<GmailMailboxMessageSummary>(selected.Count);
        foreach (var id in selected) {
            var summary = await TryGetMessageSummaryAsync(id, cancellationToken).ConfigureAwait(false);
            if (summary != null) {
                summaries.Add(summary);
            }
        }

        summaries.Sort(static (a, b) => b.DateUtc.CompareTo(a.DateUtc));
        return new GmailMailboxSearchResult {
            ResolvedLabelId = resolvedLabelId,
            Messages = summaries
        };
    }

    /// <summary>
    /// Builds a Gmail query string from mailbox search filters.
    /// </summary>
    public static string BuildSearchQuery(GmailMailboxSearchRequest request) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var parts = new List<string>();
        void Add(string? value) {
            var normalized = NormalizeOptional(value);
            if (normalized != null) {
                parts.Add(normalized);
            }
        }

        if (request.UnseenOnly) {
            parts.Add("is:unread");
        }
        if (request.HasAttachment) {
            parts.Add("has:attachment");
        }

        if (request.SinceUtc.HasValue) {
            var sinceUtc = DateTime.SpecifyKind(request.SinceUtc.Value, DateTimeKind.Utc);
            var after = new DateTimeOffset(sinceUtc).ToUnixTimeSeconds();
            parts.Add("after:" + after.ToString(CultureInfo.InvariantCulture));
        }
        if (request.BeforeUtc.HasValue) {
            var beforeUtc = DateTime.SpecifyKind(request.BeforeUtc.Value, DateTimeKind.Utc);
            var before = new DateTimeOffset(beforeUtc).ToUnixTimeSeconds();
            parts.Add("before:" + before.ToString(CultureInfo.InvariantCulture));
        }

        var subjectContains = NormalizeOptional(request.SubjectContains);
        if (!string.IsNullOrWhiteSpace(subjectContains)) {
            Add("subject:(" + subjectContains + ")");
        }
        var fromContains = NormalizeOptional(request.FromContains);
        if (!string.IsNullOrWhiteSpace(fromContains)) {
            Add("from:(" + fromContains + ")");
        }
        var toContains = NormalizeOptional(request.ToContains);
        if (!string.IsNullOrWhiteSpace(toContains)) {
            Add("to:(" + toContains + ")");
        }
        if (!string.IsNullOrWhiteSpace(request.BodyContains)) {
            Add(request.BodyContains);
        }
        if (!string.IsNullOrWhiteSpace(request.Query)) {
            Add(request.Query);
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Gets one message summary.
    /// </summary>
    public async Task<GmailMailboxMessageSummary> GetMessageSummaryAsync(
        string messageId,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        var message = await _gmail.GetFullAsync(
            _userId,
            messageId.Trim(),
            fields: MessageSummaryFields,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return MapSummary(message);
    }

    /// <summary>
    /// Gets one message content by downloading MIME payload and parsing it to <see cref="MimeMessage"/>.
    /// </summary>
    public async Task<GmailMailboxGetResult> GetMessageContentAsync(
        string messageId,
        int maxMimeBytes = DefaultMaxMimeBytes,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        if (maxMimeBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxMimeBytes), "maxMimeBytes must be greater than zero.");
        }

        var message = await _gmail.GetRawAsync(
            _userId,
            messageId.Trim(),
            fields: RawFields,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(message.Raw)) {
            throw new InvalidDataException("Gmail message response did not contain raw RFC822 payload.");
        }

        byte[] mimeBytes;
        try {
            mimeBytes = Base64UrlDecode(message.Raw!);
        } catch (Exception ex) {
            throw new InvalidDataException("Failed to decode Gmail raw MIME payload.", ex);
        }

        if (mimeBytes.Length > maxMimeBytes) {
            throw new InvalidOperationException("Gmail raw message exceeds " + maxMimeBytes.ToString(CultureInfo.InvariantCulture) + " bytes.");
        }

        MimeMessage mimeMessage;
        try {
            mimeMessage = MimeMessage.Load(new MemoryStream(mimeBytes, writable: false));
        } catch (Exception ex) {
            throw new InvalidDataException("Failed to parse Gmail MIME message.", ex);
        }

        return new GmailMailboxGetResult {
            Message = mimeMessage,
            Seen = !HasLabel(message.LabelIds, "UNREAD"),
            Flagged = HasLabel(message.LabelIds, "STARRED")
        };
    }

    /// <summary>
    /// Sets read/unread state on a single message.
    /// </summary>
    public async Task SetMessageSeenAsync(
        string messageId,
        bool seen,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        await _gmail.ModifyMessageLabelsAsync(
            _userId,
            messageId.Trim(),
            addLabelIds: seen ? Array.Empty<string>() : new[] { "UNREAD" },
            removeLabelIds: seen ? new[] { "UNREAD" } : Array.Empty<string>(),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets flagged/unflagged state on a single message.
    /// </summary>
    public async Task SetMessageFlaggedAsync(
        string messageId,
        bool flagged,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        await _gmail.ModifyMessageLabelsAsync(
            _userId,
            messageId.Trim(),
            addLabelIds: flagged ? new[] { "STARRED" } : Array.Empty<string>(),
            removeLabelIds: flagged ? Array.Empty<string>() : new[] { "STARRED" },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves a single message to a target folder/label.
    /// </summary>
    public async Task MoveMessageAsync(
        string messageId,
        string? sourceFolder,
        string targetFolder,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        if (string.IsNullOrWhiteSpace(targetFolder)) {
            throw new ArgumentException("targetFolder is required.", nameof(targetFolder));
        }

        var targetLabelId = NormalizeOptional(await ResolveLabelIdAsync(targetFolder, cancellationToken).ConfigureAwait(false));
        if (targetLabelId == null) {
            throw new InvalidOperationException("Unable to resolve Gmail target folder/label.");
        }

        if (targetLabelId.Equals("TRASH", StringComparison.OrdinalIgnoreCase)) {
            _ = await _gmail.TrashMessageAsync(_userId, messageId.Trim(), cancellationToken).ConfigureAwait(false);
            return;
        }

        var remove = new List<string>();
        var sourceLabelId = NormalizeOptional(await ResolveLabelIdAsync(sourceFolder, cancellationToken).ConfigureAwait(false));
        if (sourceLabelId != null) {
            remove.Add(sourceLabelId);
        }
        remove.Add("TRASH");

        _ = await _gmail.ModifyMessageLabelsAsync(
            _userId,
            messageId.Trim(),
            addLabelIds: new[] { targetLabelId },
            removeLabelIds: remove,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Archives a single message (removes INBOX/TRASH labels).
    /// </summary>
    public async Task ArchiveMessageAsync(
        string messageId,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        _ = await _gmail.ModifyMessageLabelsAsync(
            _userId,
            messageId.Trim(),
            addLabelIds: Array.Empty<string>(),
            removeLabelIds: new[] { "INBOX", "TRASH" },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves a single message to trash.
    /// </summary>
    public async Task TrashMessageAsync(
        string messageId,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        _ = await _gmail.TrashMessageAsync(_userId, messageId.Trim(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a single message.
    /// </summary>
    public async Task DeleteMessageAsync(
        string messageId,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        await _gmail.DeleteAsync(_userId, messageId.Trim(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets read/unread state on many messages.
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> SetMessagesSeenAsync(
        IEnumerable<string> messageIds,
        bool seen,
        int batchSize = BatchMaxIds,
        CancellationToken cancellationToken = default) {
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        return await ExecuteBatchModifyAsync(
            messageIds,
            addLabelIds: seen ? Array.Empty<string>() : new[] { "UNREAD" },
            removeLabelIds: seen ? new[] { "UNREAD" } : Array.Empty<string>(),
            batchSize,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets flagged/unflagged state on many messages.
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> SetMessagesFlaggedAsync(
        IEnumerable<string> messageIds,
        bool flagged,
        int batchSize = BatchMaxIds,
        CancellationToken cancellationToken = default) {
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        return await ExecuteBatchModifyAsync(
            messageIds,
            addLabelIds: flagged ? new[] { "STARRED" } : Array.Empty<string>(),
            removeLabelIds: flagged ? Array.Empty<string>() : new[] { "STARRED" },
            batchSize,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves many messages to a target folder/label.
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> MoveMessagesAsync(
        IEnumerable<string> messageIds,
        string? sourceFolder,
        string targetFolder,
        int batchSize = BatchMaxIds,
        CancellationToken cancellationToken = default) {
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }
        if (string.IsNullOrWhiteSpace(targetFolder)) {
            throw new ArgumentException("targetFolder is required.", nameof(targetFolder));
        }

        var ids = NormalizeIds(messageIds);
        if (ids.Count == 0) {
            return Array.Empty<GmailMailboxBulkOperationResult>();
        }

        var targetLabelId = NormalizeOptional(await ResolveLabelIdAsync(targetFolder, cancellationToken).ConfigureAwait(false));
        if (targetLabelId == null) {
            throw new InvalidOperationException("Unable to resolve Gmail target folder/label.");
        }

        if (targetLabelId.Equals("TRASH", StringComparison.OrdinalIgnoreCase)) {
            return await TrashMessagesAsync(ids, cancellationToken).ConfigureAwait(false);
        }

        var remove = new List<string>();
        var sourceLabelId = NormalizeOptional(await ResolveLabelIdAsync(sourceFolder, cancellationToken).ConfigureAwait(false));
        if (sourceLabelId != null) {
            remove.Add(sourceLabelId);
        }
        remove.Add("TRASH");

        return await ExecuteBatchModifyAsync(
            ids,
            addLabelIds: new[] { targetLabelId },
            removeLabelIds: remove,
            batchSize,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Archives many messages (removes INBOX/TRASH labels).
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> ArchiveMessagesAsync(
        IEnumerable<string> messageIds,
        int batchSize = BatchMaxIds,
        CancellationToken cancellationToken = default) {
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        return await ExecuteBatchModifyAsync(
            messageIds,
            addLabelIds: Array.Empty<string>(),
            removeLabelIds: new[] { "INBOX", "TRASH" },
            batchSize,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves many messages to trash.
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> TrashMessagesAsync(
        IEnumerable<string> messageIds,
        CancellationToken cancellationToken = default) {
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        var ids = NormalizeIds(messageIds);
        if (ids.Count == 0) {
            return Array.Empty<GmailMailboxBulkOperationResult>();
        }

        var results = new List<GmailMailboxBulkOperationResult>(ids.Count);
        foreach (var id in ids) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                _ = await _gmail.TrashMessageAsync(_userId, id, cancellationToken).ConfigureAwait(false);
                results.Add(new GmailMailboxBulkOperationResult { Id = id, Ok = true });
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                results.Add(new GmailMailboxBulkOperationResult { Id = id, Ok = false, Error = ex.Message });
            }
        }

        return results;
    }

    /// <summary>
    /// Deletes many messages.
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> DeleteMessagesAsync(
        IEnumerable<string> messageIds,
        int batchSize = BatchMaxIds,
        CancellationToken cancellationToken = default) {
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        var ids = NormalizeIds(messageIds);
        if (ids.Count == 0) {
            return Array.Empty<GmailMailboxBulkOperationResult>();
        }

        var results = new List<GmailMailboxBulkOperationResult>(ids.Count);
        foreach (var chunk in Chunk(ids, ClampInt(batchSize, 1, BatchMaxIds))) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                await _gmail.BatchDeleteMessagesAsync(_userId, chunk, cancellationToken).ConfigureAwait(false);
                foreach (var id in chunk) {
                    results.Add(new GmailMailboxBulkOperationResult { Id = id, Ok = true });
                }
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                foreach (var id in chunk) {
                    results.Add(new GmailMailboxBulkOperationResult { Id = id, Ok = false, Error = ex.Message });
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Archives a single thread (removes INBOX/TRASH labels).
    /// </summary>
    public async Task ArchiveThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(threadId)) {
            throw new ArgumentException("threadId is required.", nameof(threadId));
        }

        _ = await _gmail.ModifyThreadLabelsAsync(
            _userId,
            threadId.Trim(),
            addLabelIds: Array.Empty<string>(),
            removeLabelIds: new[] { "INBOX", "TRASH" },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves a single thread to trash.
    /// </summary>
    public async Task TrashThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(threadId)) {
            throw new ArgumentException("threadId is required.", nameof(threadId));
        }

        _ = await _gmail.TrashThreadAsync(_userId, threadId.Trim(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a single thread.
    /// </summary>
    public async Task DeleteThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(threadId)) {
            throw new ArgumentException("threadId is required.", nameof(threadId));
        }

        await _gmail.DeleteThreadAsync(_userId, threadId.Trim(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Archives many threads.
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> ArchiveThreadsAsync(
        IEnumerable<string> threadIds,
        CancellationToken cancellationToken = default) {
        return await ExecuteThreadActionAsync(threadIds, ArchiveThreadAsync, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves many threads to trash.
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> TrashThreadsAsync(
        IEnumerable<string> threadIds,
        CancellationToken cancellationToken = default) {
        return await ExecuteThreadActionAsync(threadIds, TrashThreadAsync, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes many threads.
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> DeleteThreadsAsync(
        IEnumerable<string> threadIds,
        CancellationToken cancellationToken = default) {
        return await ExecuteThreadActionAsync(threadIds, DeleteThreadAsync, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets mailbox profile.
    /// </summary>
    public async Task<GmailMailboxProfileResult> GetProfileAsync(CancellationToken cancellationToken = default) {
        var profile = await _gmail.GetProfileAsync(_userId, cancellationToken).ConfigureAwait(false);
        return new GmailMailboxProfileResult {
            EmailAddress = NormalizeOptional(profile.EmailAddress),
            MessagesTotal = profile.MessagesTotal,
            ThreadsTotal = profile.ThreadsTotal,
            HistoryId = NormalizeOptional(profile.HistoryId)
        };
    }

    /// <summary>
    /// Starts Gmail watch subscription for selected folders.
    /// </summary>
    public async Task<GmailMailboxWatchResult> WatchAsync(
        string topicName,
        IReadOnlyCollection<string>? folders,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(topicName)) {
            throw new ArgumentException("topicName is required.", nameof(topicName));
        }

        var folderInput = folders == null || folders.Count == 0
            ? new[] { "INBOX" }
            : folders;

        var labelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in folderInput) {
            if (string.IsNullOrWhiteSpace(raw)) {
                continue;
            }
            var labelId = await ResolveWatchLabelIdAsync(raw, cancellationToken).ConfigureAwait(false);
            var normalizedLabelId = NormalizeOptional(labelId);
            if (normalizedLabelId != null) {
                labelIds.Add(normalizedLabelId);
            }
        }

        var watch = await _gmail.WatchAsync(
            _userId,
            topicName.Trim(),
            labelIds.Count == 0 ? null : labelIds.ToArray(),
            cancellationToken).ConfigureAwait(false);

        DateTime? expirationUtc = null;
        if (watch.Expiration > 0) {
            expirationUtc = DateTimeOffset.FromUnixTimeMilliseconds(watch.Expiration).UtcDateTime;
        }

        return new GmailMailboxWatchResult {
            HistoryId = NormalizeOptional(watch.HistoryId),
            ExpirationUtc = expirationUtc,
            LabelIds = labelIds.ToList()
        };
    }

    /// <summary>
    /// Stops Gmail watch subscription.
    /// </summary>
    public Task StopWatchAsync(CancellationToken cancellationToken = default) =>
        _gmail.StopWatchAsync(_userId, cancellationToken);

    /// <summary>
    /// Gets Gmail history changes for a folder.
    /// </summary>
    public async Task<GmailMailboxHistoryResult> GetHistoryAsync(
        string folder,
        string startHistoryId,
        int maxChanges,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(startHistoryId)) {
            throw new ArgumentException("startHistoryId is required.", nameof(startHistoryId));
        }

        var resolvedLabelId = NormalizeOptional(await ResolveLabelIdAsync(folder, cancellationToken).ConfigureAwait(false));
        if (resolvedLabelId == null) {
            throw new InvalidOperationException("Unable to resolve Gmail folder/label.");
        }

        var max = ClampInt(maxChanges, 1, 2000);
        var upserts = new HashSet<string>(StringComparer.Ordinal);
        var deletes = new HashSet<string>(StringComparer.Ordinal);
        var historyTypes = new[] { "messageAdded", "messageDeleted", "labelAdded", "labelRemoved" };
        string? pageToken = null;
        string? newHistoryId = null;
        var pageCount = 0;

        while (pageCount++ < 25 && (upserts.Count + deletes.Count) < max) {
            var history = await _gmail.ListHistoryAsync(
                _userId,
                startHistoryId.Trim(),
                labelId: resolvedLabelId,
                historyTypes: historyTypes,
                maxResults: 500,
                pageToken: pageToken,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var historyId = NormalizeOptional(history.HistoryId);
            if (historyId != null) {
                newHistoryId = historyId;
            }

            pageToken = NormalizeOptional(history.NextPageToken);
            if (history.History == null || history.History.Count == 0) {
                break;
            }

            foreach (var entry in history.History) {
                if (entry == null) {
                    continue;
                }

                AddHistoryRefs(entry.MessagesAdded, upserts);
                AddHistoryRefs(entry.LabelsAdded, upserts);
                AddHistoryRefs(entry.MessagesDeleted, deletes);
                AddHistoryRefs(entry.LabelsRemoved, deletes);
            }

            if (string.IsNullOrWhiteSpace(pageToken)) {
                break;
            }
        }

        foreach (var deletedId in deletes) {
            _ = upserts.Remove(deletedId);
        }

        var upsertIds = upserts.ToList();
        upsertIds.Sort(StringComparer.Ordinal);
        var deleteIds = deletes.ToList();
        deleteIds.Sort(StringComparer.Ordinal);

        return new GmailMailboxHistoryResult {
            ResolvedLabelId = resolvedLabelId,
            NewHistoryId = newHistoryId,
            UpsertNativeIds = upsertIds,
            DeletedNativeIds = deleteIds
        };
    }

    private async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> ExecuteBatchModifyAsync(
        IEnumerable<string> messageIds,
        IReadOnlyCollection<string> addLabelIds,
        IReadOnlyCollection<string> removeLabelIds,
        int batchSize,
        CancellationToken cancellationToken) {
        var ids = NormalizeIds(messageIds);
        if (ids.Count == 0) {
            return Array.Empty<GmailMailboxBulkOperationResult>();
        }

        var results = new List<GmailMailboxBulkOperationResult>(ids.Count);
        foreach (var chunk in Chunk(ids, ClampInt(batchSize, 1, BatchMaxIds))) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                await _gmail.BatchModifyMessagesAsync(
                    _userId,
                    chunk,
                    addLabelIds: addLabelIds,
                    removeLabelIds: removeLabelIds,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                foreach (var id in chunk) {
                    results.Add(new GmailMailboxBulkOperationResult { Id = id, Ok = true });
                }
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                foreach (var id in chunk) {
                    results.Add(new GmailMailboxBulkOperationResult { Id = id, Ok = false, Error = ex.Message });
                }
            }
        }

        return results;
    }

    private async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> ExecuteThreadActionAsync(
        IEnumerable<string> threadIds,
        Func<string, CancellationToken, Task> actionAsync,
        CancellationToken cancellationToken) {
        if (threadIds == null) {
            throw new ArgumentNullException(nameof(threadIds));
        }
        if (actionAsync == null) {
            throw new ArgumentNullException(nameof(actionAsync));
        }

        var ids = NormalizeIds(threadIds);
        if (ids.Count == 0) {
            return Array.Empty<GmailMailboxBulkOperationResult>();
        }

        var results = new List<GmailMailboxBulkOperationResult>(ids.Count);
        foreach (var id in ids) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                await actionAsync(id, cancellationToken).ConfigureAwait(false);
                results.Add(new GmailMailboxBulkOperationResult { Id = id, Ok = true });
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                results.Add(new GmailMailboxBulkOperationResult { Id = id, Ok = false, Error = ex.Message });
            }
        }

        return results;
    }

    private static List<string> NormalizeIds(IEnumerable<string> ids) {
        var output = new List<string>();
        if (ids == null) {
            return output;
        }

        foreach (var raw in ids) {
            var id = NormalizeOptional(raw);
            if (id != null) {
                output.Add(id);
            }
        }

        return output;
    }

    private static IEnumerable<List<string>> Chunk(IReadOnlyList<string> ids, int chunkSize) {
        var safeChunkSize = chunkSize <= 0 ? 1 : chunkSize;
        for (var i = 0; i < ids.Count; i += safeChunkSize) {
            var count = Math.Min(safeChunkSize, ids.Count - i);
            var chunk = new List<string>(count);
            for (var j = 0; j < count; j++) {
                chunk.Add(ids[i + j]);
            }
            yield return chunk;
        }
    }

    private static void AddHistoryRefs(
        IReadOnlyCollection<GmailApiClient.GmailHistoryMessageAdded>? refs,
        HashSet<string> output) {
        if (refs == null || refs.Count == 0) {
            return;
        }

        foreach (var entry in refs) {
            var id = NormalizeOptional(entry?.Message?.Id);
            if (id != null) {
                output.Add(id);
            }
        }
    }

    private static void AddHistoryRefs(
        IReadOnlyCollection<GmailApiClient.GmailHistoryLabelAdded>? refs,
        HashSet<string> output) {
        if (refs == null || refs.Count == 0) {
            return;
        }

        foreach (var entry in refs) {
            var id = NormalizeOptional(entry?.Message?.Id);
            if (id != null) {
                output.Add(id);
            }
        }
    }

    private static void AddHistoryRefs(
        IReadOnlyCollection<GmailApiClient.GmailHistoryMessageDeleted>? refs,
        HashSet<string> output) {
        if (refs == null || refs.Count == 0) {
            return;
        }

        foreach (var entry in refs) {
            var id = NormalizeOptional(entry?.Message?.Id);
            if (id != null) {
                output.Add(id);
            }
        }
    }

    private static void AddHistoryRefs(
        IReadOnlyCollection<GmailApiClient.GmailHistoryLabelRemoved>? refs,
        HashSet<string> output) {
        if (refs == null || refs.Count == 0) {
            return;
        }

        foreach (var entry in refs) {
            var id = NormalizeOptional(entry?.Message?.Id);
            if (id != null) {
                output.Add(id);
            }
        }
    }

    private async Task<GmailMailboxMessageSummary?> TryGetMessageSummaryAsync(string messageId, CancellationToken cancellationToken) {
        try {
            return await GetMessageSummaryAsync(messageId, cancellationToken).ConfigureAwait(false);
        } catch {
            return null;
        }
    }

    private static bool TryResolveSystemLabel(string raw, out string labelId) {
        var normalized = raw.Trim();
        if (normalized.Equals("INBOX", StringComparison.OrdinalIgnoreCase)) {
            labelId = "INBOX";
            return true;
        }
        if (normalized.Equals("SENT", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("SENTITEMS", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("SENT ITEMS", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("SENT MAIL", StringComparison.OrdinalIgnoreCase)) {
            labelId = "SENT";
            return true;
        }
        if (normalized.Equals("TRASH", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("DELETED", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("DELETED ITEMS", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("DELETEDITEMS", StringComparison.OrdinalIgnoreCase)) {
            labelId = "TRASH";
            return true;
        }
        if (normalized.Equals("DRAFT", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("DRAFTS", StringComparison.OrdinalIgnoreCase)) {
            labelId = "DRAFT";
            return true;
        }
        if (normalized.Equals("SPAM", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("JUNK", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("JUNK EMAIL", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("JUNKEMAIL", StringComparison.OrdinalIgnoreCase)) {
            labelId = "SPAM";
            return true;
        }
        if (normalized.Equals("STARRED", StringComparison.OrdinalIgnoreCase)) {
            labelId = "STARRED";
            return true;
        }
        if (normalized.Equals("IMPORTANT", StringComparison.OrdinalIgnoreCase)) {
            labelId = "IMPORTANT";
            return true;
        }

        labelId = string.Empty;
        return false;
    }

    private static bool TryResolveWatchSystemLabel(string raw, out string labelId) {
        if (raw.Equals("INBOX", StringComparison.OrdinalIgnoreCase)) {
            labelId = "INBOX";
            return true;
        }
        if (raw.Equals("SENT", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("SENTITEMS", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("SENT ITEMS", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("SENT MAIL", StringComparison.OrdinalIgnoreCase)) {
            labelId = "SENT";
            return true;
        }
        if (raw.Equals("TRASH", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("DELETED", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("DELETED ITEMS", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("DELETEDITEMS", StringComparison.OrdinalIgnoreCase)) {
            labelId = "TRASH";
            return true;
        }
        if (raw.Equals("SPAM", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("JUNK", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("JUNK EMAIL", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("JUNKEMAIL", StringComparison.OrdinalIgnoreCase)) {
            labelId = "SPAM";
            return true;
        }
        if (raw.Equals("DRAFT", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("DRAFTS", StringComparison.OrdinalIgnoreCase)) {
            labelId = "DRAFT";
            return true;
        }

        labelId = string.Empty;
        return false;
    }

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
    /// Gmail mailbox get-message result.
    /// </summary>
    public sealed class GmailMailboxGetResult {
        /// <summary>Parsed MIME message.</summary>
        public MimeMessage Message { get; set; } = new MimeMessage();

        /// <summary>Read state from Gmail labels.</summary>
        public bool? Seen { get; set; }

        /// <summary>Flagged state from Gmail labels.</summary>
        public bool? Flagged { get; set; }
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
    /// Gmail mailbox history result.
    /// </summary>
    public sealed class GmailMailboxHistoryResult {
        /// <summary>Resolved Gmail label id used for history query.</summary>
        public string ResolvedLabelId { get; set; } = string.Empty;

        /// <summary>Newest history id seen while reading history pages.</summary>
        public string? NewHistoryId { get; set; }

        /// <summary>Message ids that should be upserted.</summary>
        public List<string> UpsertNativeIds { get; set; } = new();

        /// <summary>Message ids that should be deleted.</summary>
        public List<string> DeletedNativeIds { get; set; } = new();
    }

    /// <summary>
    /// Gmail mailbox bulk action result.
    /// </summary>
    public sealed class GmailMailboxBulkOperationResult {
        /// <summary>Message/thread id.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>True when action succeeded.</summary>
        public bool Ok { get; set; }

        /// <summary>Error message when action failed.</summary>
        public string? Error { get; set; }
    }

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
