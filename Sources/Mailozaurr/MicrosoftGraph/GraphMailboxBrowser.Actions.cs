using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

public sealed partial class GraphMailboxBrowser {
    /// <summary>
    /// Reads provider threading metadata for a single message.
    /// </summary>
    /// <param name="messageId">Graph message id.</param>
    /// <param name="maxMimeBytes">Maximum MIME payload size to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Threading metadata parsed from MIME headers.</returns>
    public async Task<GraphMailboxThreadingMetadataResult> GetThreadingMetadataAsync(
        string messageId,
        int maxMimeBytes = DefaultThreadingMetadataMaxMimeBytes,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        if (maxMimeBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxMimeBytes), "maxMimeBytes must be greater than zero.");
        }

        var mimeBytes = await _graph.GetMessageMimeAsync(
            messageId.Trim(),
            maxBytes: maxMimeBytes,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        MimeMessage message;
        try {
            message = MimeMessage.Load(new MemoryStream(mimeBytes, writable: false));
        } catch (Exception ex) {
            throw new InvalidDataException("Failed to parse Graph MIME message.", ex);
        }

        return new GraphMailboxThreadingMetadataResult {
            MessageId = NormalizeMessageIdValue(message.MessageId),
            ReplyTo = NormalizeOptional(message.ReplyTo?.ToString()),
            Cc = NormalizeOptional(message.Cc?.ToString()),
            InReplyTo = NormalizeMessageIdValue(message.InReplyTo),
            References = NormalizeMessageIdValues(message.References)
        };
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
        var meta = await _graph.GetMessageAsync(normalizedId, select: "id,isRead,flag,conversationId", cancellationToken: cancellationToken).ConfigureAwait(false);
        if (meta == null) {
            throw new InvalidDataException("Graph message metadata was not returned.");
        }
        var mimeBytes = await _graph.GetMessageMimeAsync(normalizedId, maxBytes: maxMimeBytes, cancellationToken: cancellationToken).ConfigureAwait(false);
        string? conversationId = null;
        var trimmedConversationId = meta.ConversationId?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedConversationId)) {
            conversationId = trimmedConversationId;
        }
        try {
            return new GraphMailboxGetResult {
                Seen = meta.IsRead,
                Flagged = meta.Flag == null ? null : IsFlagged(meta.Flag),
                NativeThreadId = conversationId,
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
    /// Archives a single message.
    /// </summary>
    public Task ArchiveMessageAsync(
        string messageId,
        CancellationToken cancellationToken = default) {
        return MoveMessageAsync(messageId, "Archive", cancellationToken);
    }

    /// <summary>
    /// Moves a single message to trash.
    /// </summary>
    public Task TrashMessageAsync(
        string messageId,
        CancellationToken cancellationToken = default) {
        return MoveMessageAsync(messageId, "Trash", cancellationToken);
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

        var ids = NormalizeBulkIds(messageIds);
        if (ids.Count == 0) {
            return Array.Empty<GraphBulkOperationResult>();
        }

        var destinationId = await ResolveFolderIdAsync(targetFolder, cancellationToken).ConfigureAwait(false);
        return await _graph.BatchMoveMessagesAsync(
            ids,
            destinationId,
            batchSize: batchSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Archives many messages.
    /// </summary>
    public Task<IReadOnlyList<GraphBulkOperationResult>> ArchiveMessagesAsync(
        IEnumerable<string> messageIds,
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        return MoveMessagesAsync(messageIds, "Archive", batchSize, cancellationToken);
    }

    /// <summary>
    /// Moves many messages to trash.
    /// </summary>
    public Task<IReadOnlyList<GraphBulkOperationResult>> TrashMessagesAsync(
        IEnumerable<string> messageIds,
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        return MoveMessagesAsync(messageIds, "Trash", batchSize, cancellationToken);
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
    /// Sets read/unread state on many conversations.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> SetConversationsSeenAsync(
        IEnumerable<string> conversationIds,
        bool seen,
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        if (conversationIds == null) {
            throw new ArgumentNullException(nameof(conversationIds));
        }

        return await ExecuteConversationMessageActionAsync(
            conversationIds,
            (messageIds, token) => _graph.BatchSetMessagesIsReadAsync(
                messageIds,
                seen,
                batchSize: batchSize,
                cancellationToken: token),
            "Graph conversation set-seen failed.",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets flagged/unflagged state on many conversations.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> SetConversationsFlaggedAsync(
        IEnumerable<string> conversationIds,
        bool flagged,
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        if (conversationIds == null) {
            throw new ArgumentNullException(nameof(conversationIds));
        }

        return await ExecuteConversationMessageActionAsync(
            conversationIds,
            (messageIds, token) => _graph.BatchSetMessagesFlaggedAsync(
                messageIds,
                flagged,
                batchSize: batchSize,
                cancellationToken: token),
            "Graph conversation set-flagged failed.",
            cancellationToken).ConfigureAwait(false);
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

        var ids = NormalizeBulkIds(conversationIds);
        if (ids.Count == 0) {
            return Array.Empty<GraphBulkOperationResult>();
        }

        var destinationId = await ResolveFolderIdAsync(targetFolder, cancellationToken).ConfigureAwait(false);
        return await _graph.BatchMoveConversationsAsync(
            ids,
            destinationId,
            batchSize: batchSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Archives many conversations.
    /// </summary>
    public Task<IReadOnlyList<GraphBulkOperationResult>> ArchiveConversationsAsync(
        IEnumerable<string> conversationIds,
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        return MoveConversationsAsync(conversationIds, "Archive", batchSize, cancellationToken);
    }

    /// <summary>
    /// Moves many conversations to trash.
    /// </summary>
    public Task<IReadOnlyList<GraphBulkOperationResult>> TrashConversationsAsync(
        IEnumerable<string> conversationIds,
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        return MoveConversationsAsync(conversationIds, "Trash", batchSize, cancellationToken);
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
}