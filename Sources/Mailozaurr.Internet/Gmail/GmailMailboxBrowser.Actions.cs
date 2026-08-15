using MimeKit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

public sealed partial class GmailMailboxBrowser {
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
        var sourceLabelId = await ResolveSourceLabelIdAsync(sourceFolder, cancellationToken).ConfigureAwait(false);
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
    /// Sets read/unread state on a single thread.
    /// </summary>
    public async Task SetThreadSeenAsync(
        string threadId,
        bool seen,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(threadId)) {
            throw new ArgumentException("threadId is required.", nameof(threadId));
        }

        _ = await _gmail.ModifyThreadLabelsAsync(
            _userId,
            threadId.Trim(),
            addLabelIds: seen ? Array.Empty<string>() : new[] { "UNREAD" },
            removeLabelIds: seen ? new[] { "UNREAD" } : Array.Empty<string>(),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets flagged/unflagged state on a single thread.
    /// </summary>
    public async Task SetThreadFlaggedAsync(
        string threadId,
        bool flagged,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(threadId)) {
            throw new ArgumentException("threadId is required.", nameof(threadId));
        }

        _ = await _gmail.ModifyThreadLabelsAsync(
            _userId,
            threadId.Trim(),
            addLabelIds: flagged ? new[] { "STARRED" } : Array.Empty<string>(),
            removeLabelIds: flagged ? Array.Empty<string>() : new[] { "STARRED" },
            cancellationToken: cancellationToken).ConfigureAwait(false);
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
        var sourceLabelId = await ResolveSourceLabelIdAsync(sourceFolder, cancellationToken).ConfigureAwait(false);
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
    /// Moves a single thread to a target folder/label.
    /// </summary>
    public async Task MoveThreadAsync(
        string threadId,
        string? sourceFolder,
        string targetFolder,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(threadId)) {
            throw new ArgumentException("threadId is required.", nameof(threadId));
        }
        if (string.IsNullOrWhiteSpace(targetFolder)) {
            throw new ArgumentException("targetFolder is required.", nameof(targetFolder));
        }

        var targetRaw = targetFolder.Trim();
        if (targetRaw.Equals("Archive", StringComparison.OrdinalIgnoreCase)) {
            await ArchiveThreadAsync(threadId, cancellationToken).ConfigureAwait(false);
            return;
        }

        var targetLabelId = NormalizeOptional(await ResolveLabelIdAsync(targetRaw, cancellationToken).ConfigureAwait(false));
        if (targetLabelId == null) {
            throw new InvalidOperationException("Unable to resolve Gmail target folder/label.");
        }

        if (targetLabelId.Equals("TRASH", StringComparison.OrdinalIgnoreCase)) {
            await TrashThreadAsync(threadId, cancellationToken).ConfigureAwait(false);
            return;
        }

        var remove = new List<string>();
        var sourceLabelId = NormalizeOptional(await ResolveLabelIdAsync(sourceFolder, cancellationToken).ConfigureAwait(false));
        if (sourceLabelId != null) {
            remove.Add(sourceLabelId);
        }
        remove.Add("TRASH");

        _ = await _gmail.ModifyThreadLabelsAsync(
            _userId,
            threadId.Trim(),
            addLabelIds: new[] { targetLabelId },
            removeLabelIds: remove,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves many threads to a target folder/label.
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> MoveThreadsAsync(
        IEnumerable<string> threadIds,
        string? sourceFolder,
        string targetFolder,
        CancellationToken cancellationToken = default) {
        return await ExecuteThreadActionAsync(
            threadIds,
            (threadId, token) => MoveThreadAsync(threadId, sourceFolder, targetFolder, token),
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
    /// Sets read/unread state on many threads.
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> SetThreadsSeenAsync(
        IEnumerable<string> threadIds,
        bool seen,
        CancellationToken cancellationToken = default) {
        return await ExecuteThreadActionAsync(
            threadIds,
            (threadId, token) => SetThreadSeenAsync(threadId, seen, token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets flagged/unflagged state on many threads.
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> SetThreadsFlaggedAsync(
        IEnumerable<string> threadIds,
        bool flagged,
        CancellationToken cancellationToken = default) {
        return await ExecuteThreadActionAsync(
            threadIds,
            (threadId, token) => SetThreadFlaggedAsync(threadId, flagged, token),
            cancellationToken).ConfigureAwait(false);
    }
}