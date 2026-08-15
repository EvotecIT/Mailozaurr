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
    private async Task<IReadOnlyList<GraphBulkOperationResult>> ExecuteConversationMessageActionAsync(
        IEnumerable<string> conversationIds,
        Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<GraphBulkOperationResult>>> operationAsync,
        string fallbackError,
        CancellationToken cancellationToken) {
        if (conversationIds == null) {
            throw new ArgumentNullException(nameof(conversationIds));
        }
        if (operationAsync == null) {
            throw new ArgumentNullException(nameof(operationAsync));
        }

        var conversations = NormalizeConversationIds(conversationIds);
        if (conversations.Count == 0) {
            return Array.Empty<GraphBulkOperationResult>();
        }

        var output = new List<GraphBulkOperationResult>(conversations.Count);
        foreach (var conversationId in conversations) {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<string> messageIds;
            try {
                messageIds = await _graph.ListConversationMessageIdsAsync(
                    conversationId,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                output.Add(new GraphBulkOperationResult {
                    Id = conversationId,
                    Ok = false,
                    Error = ex.Message
                });
                continue;
            }

            if (messageIds.Count == 0) {
                output.Add(new GraphBulkOperationResult { Id = conversationId, Ok = true });
                continue;
            }

            var results = await operationAsync(messageIds, cancellationToken).ConfigureAwait(false);
            var failed = results.FirstOrDefault(result => result != null && !result.Ok);
            if (failed is not null) {
                output.Add(new GraphBulkOperationResult {
                    Id = conversationId,
                    Ok = false,
                    Error = failed.Error ?? fallbackError
                });
                continue;
            }

            output.Add(new GraphBulkOperationResult { Id = conversationId, Ok = true });
        }

        return output;
    }

    private static List<string> NormalizeConversationIds(IEnumerable<string> conversationIds) {
        var output = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in conversationIds) {
            if (string.IsNullOrWhiteSpace(raw)) {
                continue;
            }

            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || !seen.Add(trimmed)) {
                continue;
            }

            output.Add(trimmed);
        }

        return output;
    }

    private async Task UploadLargeAttachmentsAsync(
        string messageId,
        IReadOnlyList<DecodedMimeAttachment> attachments,
        CancellationToken cancellationToken) {
        foreach (var attachment in attachments) {
            if (attachment == null) {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await UploadLargeAttachmentAsync(messageId, attachment, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task UploadLargeAttachmentAsync(
        string messageId,
        DecodedMimeAttachment attachment,
        CancellationToken cancellationToken) {
        if (attachment.Length <= 0) {
            throw new InvalidDataException(
                $"Graph upload failed: attachment '{attachment.Name}' length is {attachment.Length.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (attachment.Length < Graph.MinimumUploadSessionAttachmentSize) {
            await _graph.AddAttachmentAsync(
                messageId,
                attachment.ToGraphAttachment(),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        var uploadSession = await _graph.CreateAttachmentUploadSessionAsync(
            messageId,
            BuildAttachmentItem(attachment),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var uploadUrl = NormalizeOptional(uploadSession.UploadUrl);
        if (uploadUrl == null) {
            throw new InvalidDataException("Graph upload session creation failed (empty uploadUrl).");
        }

        using var stream = attachment.OpenRead();
        long offset = 0;
        while (offset < attachment.Length) {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = attachment.Length - offset;
            var chunkLen = (int)Math.Min(LargeAttachmentChunkSize, remaining);
            var buffer = new byte[chunkLen];

            var read = 0;
            while (read < chunkLen) {
#if NET5_0_OR_GREATER
                var count = await stream.ReadAsync(buffer.AsMemory(read, chunkLen - read), cancellationToken).ConfigureAwait(false);
#else
                var count = await stream.ReadAsync(buffer, read, chunkLen - read, cancellationToken).ConfigureAwait(false);
#endif
                if (count <= 0) {
                    break;
                }

                read += count;
            }

            if (read <= 0) {
                throw new InvalidDataException(
                    $"Graph upload failed: unexpected end of stream for '{attachment.Name}' at {offset.ToString(CultureInfo.InvariantCulture)}.");
            }

            var start = offset;
            var end = offset + read - 1;
            if (read == buffer.Length) {
                await _graph.UploadAttachmentChunkAsync(
                    uploadUrl,
                    buffer,
                    start,
                    end,
                    attachment.Length,
                    cancellationToken).ConfigureAwait(false);
            } else {
                var trimmed = new byte[read];
                Buffer.BlockCopy(buffer, 0, trimmed, 0, read);
                await _graph.UploadAttachmentChunkAsync(
                    uploadUrl,
                    trimmed,
                    start,
                    end,
                    attachment.Length,
                    cancellationToken).ConfigureAwait(false);
            }

            offset += read;
        }
    }

    private static GraphAttachmentItem BuildAttachmentItem(DecodedMimeAttachment attachment) {
        var item = new GraphAttachmentItem("file", attachment.Name, attachment.Length) {
            ContentType = NormalizeOptional(attachment.ContentType)
        };
        if (attachment.IsInline) {
            item.IsInline = true;
            item.ContentId = NormalizeOptional(attachment.ContentId);
        }
        return item;
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

    private sealed class GraphFolderPathNode {
        public GraphFolderPathNode(
            string id,
            string displayName,
            string? parentId,
            string? wellKnownName,
            int? totalItemCount,
            int? unreadItemCount) {
            Id = id;
            DisplayName = displayName;
            ParentId = parentId;
            WellKnownName = wellKnownName;
            TotalItemCount = totalItemCount;
            UnreadItemCount = unreadItemCount;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string? ParentId { get; }
        public string? WellKnownName { get; }
        public int? TotalItemCount { get; }
        public int? UnreadItemCount { get; }
    }

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

    private static string? NormalizeOptional(string? raw) {
        var trimmed = (raw ?? string.Empty).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static List<string> NormalizeBulkIds(IEnumerable<string> values) {
        var normalized = new List<string>();
        foreach (var value in values) {
            var trimmed = NormalizeOptional(value);
            if (trimmed != null) {
                normalized.Add(trimmed);
            }
        }
        return normalized;
    }

    private static string EscapeGraphLiteral(string value) =>
        (value ?? string.Empty).Replace("'", "''");

    private static string EscapeODataStringLiteral(string value) =>
        (value ?? string.Empty).Replace("'", "''");

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

    private static List<string> NormalizeMessageIdValues(IEnumerable<string>? values) {
        if (values == null) {
            return new List<string>();
        }

        var output = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values) {
            var normalized = NormalizeMessageIdValue(value);
            if (normalized == null || !seen.Add(normalized)) {
                continue;
            }
            output.Add(normalized);
        }
        return output;
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

    private static GraphMailboxSubscriptionResult MapSubscription(GraphApiClient.GraphSubscription subscription, string? resource) {
        if (subscription == null) {
            throw new ArgumentNullException(nameof(subscription));
        }

        return new GraphMailboxSubscriptionResult {
            SubscriptionId = NormalizeOptional(subscription.Id),
            Resource = NormalizeOptional(resource ?? subscription.Resource),
            ClientState = NormalizeOptional(subscription.ClientState),
            ExpirationDateTime = subscription.ExpirationDateTime
        };
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
}
