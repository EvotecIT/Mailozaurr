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
    /// Lists mailbox folders and returns normalized hierarchical folder names.
    /// </summary>
    public async Task<IReadOnlyList<GraphMailboxFolderSummary>> ListFoldersAsync(
        int top = 200,
        int maxRequests = 250,
        CancellationToken cancellationToken = default) {
        var folders = await _graph.ListMailFoldersRecursiveAsync(
            top: ClampInt(top, 1, 999),
            select: "id,displayName,parentFolderId,childFolderCount,wellKnownName,totalItemCount,unreadItemCount",
            maxRequests: ClampInt(maxRequests, 1, 5000),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var nodes = new Dictionary<string, GraphFolderPathNode>(StringComparer.Ordinal);
        foreach (var folder in folders) {
            if (folder == null) {
                continue;
            }

            var id = string.IsNullOrWhiteSpace(folder.Id) ? null : folder.Id.Trim();
            if (id == null) {
                continue;
            }

            var displayName = string.IsNullOrWhiteSpace(folder.DisplayName) ? id : folder.DisplayName.Trim();
            var parentIdRaw = folder.ParentFolderId;
            string? parentId = null;
            if (!string.IsNullOrWhiteSpace(parentIdRaw)) {
                parentId = parentIdRaw!.Trim();
            }
            var wellKnownNameRaw = folder.WellKnownName;
            string? wellKnownName = null;
            if (!string.IsNullOrWhiteSpace(wellKnownNameRaw)) {
                wellKnownName = wellKnownNameRaw!.Trim();
            }
            nodes[id] = new GraphFolderPathNode(
                id,
                displayName,
                parentId,
                wellKnownName,
                folder.TotalItemCount,
                folder.UnreadItemCount);
        }

        string BuildPath(string id) {
            if (!nodes.TryGetValue(id, out var node)) {
                return id;
            }

            var parts = new List<string>();
            var current = node;
            var guard = 0;
            while (guard++ < 100) {
                parts.Add(current.DisplayName);
                var parentId = current.ParentId;
                if (string.IsNullOrWhiteSpace(parentId)) {
                    break;
                }

                var parentKey = parentId!.Trim();
                if (!nodes.TryGetValue(parentKey, out current)) {
                    break;
                }
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        var output = new List<GraphMailboxFolderSummary>(nodes.Count);
        foreach (var node in nodes.Values) {
            output.Add(new GraphMailboxFolderSummary {
                Id = node.Id,
                Name = BuildPath(node.Id),
                DisplayName = node.DisplayName,
                ParentId = node.ParentId,
                WellKnownName = node.WellKnownName,
                TotalItemCount = node.TotalItemCount,
                UnreadItemCount = node.UnreadItemCount
            });
        }

        output.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return output;
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
    /// Lists a paged slice of messages in a Graph conversation.
    /// </summary>
    public async Task<GraphMailboxConversationListResult> ListConversationMessagesPageAsync(
        string conversationId,
        int limit,
        int offset,
        int top = 100,
        int maxPages = 25,
        int maxItems = 2000,
        CancellationToken cancellationToken = default) {
        var messages = await ListConversationMessagesAsync(
            conversationId,
            top: top,
            maxPages: maxPages,
            maxItems: maxItems,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var total = messages.Count;
        var skip = Math.Max(0, offset);
        var take = ClampInt(limit, 1, 1000);
        var page = messages.Skip(skip).Take(take).ToList();

        return new GraphMailboxConversationListResult {
            ConversationId = conversationId.Trim(),
            TotalCount = total,
            Messages = page
        };
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
    /// Imports a MIME message into a Graph folder (typically <c>sentitems</c>).
    /// </summary>
    /// <param name="message">MIME message to import.</param>
    /// <param name="folder">Folder alias/id. Defaults to <c>Sent Items</c>.</param>
    /// <param name="maxInlineAttachmentBytes">Maximum inline-attachment budget in bytes.</param>
    /// <param name="idempotencyHeaderName">Optional idempotency header name to preserve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Import result.</returns>
    public async Task<GraphMailboxImportResult> ImportMessageAsync(
        MimeMessage message,
        string? folder = "Sent Items",
        int maxInlineAttachmentBytes = GraphMimePreparation.DefaultMaxInlineAttachmentBytes,
        string? idempotencyHeaderName = null,
        CancellationToken cancellationToken = default) {
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }
        if (maxInlineAttachmentBytes < 0) {
            throw new ArgumentOutOfRangeException(nameof(maxInlineAttachmentBytes), "maxInlineAttachmentBytes must be zero or greater.");
        }

        var folderSelector = ResolveFolderSelector(folder);
        var prepared = GraphMimePreparation.PrepareMessage(
            message,
            maxInlineAttachmentBytes: maxInlineAttachmentBytes,
            idempotencyHeaderName: idempotencyHeaderName);
        try {
            var created = await _graph.CreateMessageAsync(
                prepared.Message,
                folderIdOrWellKnownName: folderSelector,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var nativeId = NormalizeOptional(created.Id);
            if (nativeId == null) {
                throw new InvalidDataException("Graph returned an invalid created message response (missing id).");
            }

            if (prepared.UploadAttachments.Count > 0) {
                await UploadLargeAttachmentsAsync(
                    nativeId,
                    prepared.UploadAttachments,
                    cancellationToken).ConfigureAwait(false);
            }

            return new GraphMailboxImportResult {
                FolderSelector = folderSelector,
                NativeId = nativeId,
                MessageId = NormalizeMessageIdValue(message.MessageId)
            };
        } finally {
            foreach (var attachment in prepared.UploadAttachments) {
                attachment.Dispose();
            }
        }
    }
}