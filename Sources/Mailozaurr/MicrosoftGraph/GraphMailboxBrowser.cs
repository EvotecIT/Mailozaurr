using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// High-level delegated-token mailbox browsing helpers built on top of <see cref="GraphApiClient"/>.
/// </summary>
public sealed class GraphMailboxBrowser {
    private const string SummarySelect = "id,subject,receivedDateTime,from,toRecipients,internetMessageId,hasAttachments,isRead,flag,conversationId";
    // Must be a multiple of 320 KiB (except last chunk).
    private const int LargeAttachmentChunkSize = 327_680 * 32; // 10 MiB
    private readonly GraphApiClient _graph;

    /// <summary>
    /// Maximum MIME payload size used by <see cref="GetMessageContentAsync"/> when no explicit limit is provided.
    /// </summary>
    public const int DefaultMaxMimeBytes = 25 * 1024 * 1024;

    /// <summary>
    /// Maximum MIME payload size used by <see cref="GetThreadingMetadataAsync"/> when no explicit limit is provided.
    /// </summary>
    public const int DefaultThreadingMetadataMaxMimeBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphMailboxBrowser"/> class.
    /// </summary>
    /// <param name="graph">Graph API client.</param>
    public GraphMailboxBrowser(GraphApiClient graph) {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
    }

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

    /// <summary>
    /// Sends a MIME message by creating a Graph draft and dispatching it.
    /// </summary>
    /// <param name="message">MIME message to send.</param>
    /// <param name="maxInlineAttachmentBytes">Maximum inline-attachment budget in bytes.</param>
    /// <param name="idempotencyHeaderName">Optional idempotency header name to preserve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Send result metadata.</returns>
    public async Task<GraphMailboxSendResult> SendMessageAsync(
        MimeMessage message,
        int maxInlineAttachmentBytes = GraphMimePreparation.DefaultMaxInlineAttachmentBytes,
        string? idempotencyHeaderName = null,
        CancellationToken cancellationToken = default) {
        var draft = await ImportMessageAsync(
            message,
            folder: "Drafts",
            maxInlineAttachmentBytes: maxInlineAttachmentBytes,
            idempotencyHeaderName: idempotencyHeaderName,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var draftId = NormalizeOptional(draft.NativeId);
        if (draftId == null) {
            throw new InvalidDataException("Graph draft created but response did not include message id.");
        }

        await _graph.SendDraftMessageAsync(draftId, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new GraphMailboxSendResult {
            DraftId = draftId,
            MessageId = draft.MessageId
        };
    }

    /// <summary>
    /// Probes a Graph folder for a message with a matching RFC822 <c>Message-Id</c> token.
    /// </summary>
    /// <param name="messageIdToken">Message-Id token (with or without angle brackets).</param>
    /// <param name="folder">Folder alias/id. Defaults to <c>Sent Items</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Duplicate probe result.</returns>
    public async Task<GraphMailboxDuplicateProbeResult> FindMessageByInternetMessageIdAsync(
        string messageIdToken,
        string? folder = "Sent Items",
        CancellationToken cancellationToken = default) {
        var normalizedToken = NormalizeMessageIdValue(messageIdToken);
        if (normalizedToken == null) {
            throw new ArgumentException("messageIdToken is required.", nameof(messageIdToken));
        }

        var folderSelector = ResolveFolderSelector(folder);
        var bracketedToken = "<" + normalizedToken + ">";
        var filter = "internetMessageId eq '" + EscapeODataStringLiteral(bracketedToken) +
                     "' or internetMessageId eq '" + EscapeODataStringLiteral(normalizedToken) + "'";
        var page = await _graph.ListMessagesAsync(
            folderSelector,
            top: 1,
            skip: null,
            select: "id,internetMessageId",
            orderBy: null,
            filter: filter,
            search: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var match = page.Items.FirstOrDefault(x =>
            string.Equals(
                NormalizeMessageIdValue(x.InternetMessageId),
                normalizedToken,
                StringComparison.OrdinalIgnoreCase));
        if (match == null) {
            return new GraphMailboxDuplicateProbeResult {
                IsMatch = false,
                FolderSelector = folderSelector
            };
        }

        return new GraphMailboxDuplicateProbeResult {
            IsMatch = true,
            FolderSelector = folderSelector,
            NativeId = NormalizeOptional(match.Id),
            MessageId = NormalizeMessageIdValue(match.InternetMessageId)
        };
    }

    /// <summary>
    /// Creates a Graph webhook subscription for message changes in a selected folder.
    /// </summary>
    public async Task<GraphMailboxSubscriptionResult> CreateMessageSubscriptionAsync(
        string notificationUrl,
        string folder = "INBOX",
        DateTimeOffset? expirationDateTime = null,
        string changeType = "created,updated,deleted",
        string? clientState = null,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(notificationUrl)) {
            throw new ArgumentException("notificationUrl is required.", nameof(notificationUrl));
        }
        if (string.IsNullOrWhiteSpace(changeType)) {
            throw new ArgumentException("changeType is required.", nameof(changeType));
        }

        var resource = BuildMessageSubscriptionResource(folder);
        var request = new GraphApiClient.GraphCreateSubscriptionRequest {
            ChangeType = changeType.Trim(),
            NotificationUrl = notificationUrl.Trim(),
            Resource = resource,
            ExpirationDateTime = expirationDateTime ?? DateTimeOffset.UtcNow.AddHours(8),
            ClientState = string.IsNullOrWhiteSpace(clientState) ? null : clientState!.Trim()
        };

        var created = await _graph.CreateSubscriptionAsync(request, cancellationToken).ConfigureAwait(false);
        return MapSubscription(created, resource);
    }

    /// <summary>
    /// Renews an existing Graph webhook subscription.
    /// </summary>
    public async Task<GraphMailboxSubscriptionResult> RenewSubscriptionAsync(
        string subscriptionId,
        DateTimeOffset expirationDateTime,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(subscriptionId)) {
            throw new ArgumentException("subscriptionId is required.", nameof(subscriptionId));
        }

        var renewed = await _graph.RenewSubscriptionAsync(
            subscriptionId.Trim(),
            expirationDateTime,
            cancellationToken).ConfigureAwait(false);
        return MapSubscription(renewed, NormalizeOptional(renewed.Resource));
    }

    /// <summary>
    /// Renews an existing Graph webhook subscription with stale-remote handling.
    /// </summary>
    public async Task<GraphMailboxSubscriptionRenewResult> RenewSubscriptionSafeAsync(
        string subscriptionId,
        DateTimeOffset expirationDateTime,
        bool treatMissingAsStale = true,
        CancellationToken cancellationToken = default) {
        try {
            var renewed = await RenewSubscriptionAsync(subscriptionId, expirationDateTime, cancellationToken).ConfigureAwait(false);
            return new GraphMailboxSubscriptionRenewResult {
                Renewed = true,
                Missing = false,
                Subscription = renewed
            };
        } catch (GraphApiException ex) when (treatMissingAsStale &&
                                             (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Gone)) {
            return new GraphMailboxSubscriptionRenewResult {
                Renewed = false,
                Missing = true,
                Subscription = null
            };
        }
    }

    /// <summary>
    /// Deletes an existing Graph webhook subscription.
    /// </summary>
    public async Task<GraphMailboxSubscriptionDeleteResult> DeleteSubscriptionAsync(
        string subscriptionId,
        bool treatMissingAsSuccess = true,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(subscriptionId)) {
            throw new ArgumentException("subscriptionId is required.", nameof(subscriptionId));
        }

        try {
            await _graph.DeleteSubscriptionAsync(subscriptionId.Trim(), cancellationToken).ConfigureAwait(false);
            return new GraphMailboxSubscriptionDeleteResult { Deleted = true };
        } catch (GraphApiException ex) when (treatMissingAsSuccess &&
                                             (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Gone)) {
            return new GraphMailboxSubscriptionDeleteResult {
                Deleted = true,
                AlreadyDeleted = true
            };
        }
    }

    /// <summary>
    /// Builds Graph subscription resource for folder message notifications.
    /// </summary>
    public static string BuildMessageSubscriptionResource(string folder) {
        var selector = ResolveFolderSelector(folder);
        return "me/mailFolders('" + EscapeGraphLiteral(selector) + "')/messages";
    }

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

        var destinationId = await ResolveFolderIdAsync(targetFolder, cancellationToken).ConfigureAwait(false);
        return await _graph.BatchMoveMessagesAsync(
            messageIds,
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

    /// <summary>
    /// Graph mailbox folder summary.
    /// </summary>
    public sealed class GraphMailboxFolderSummary {
        /// <summary>Graph folder identifier.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Hierarchical display path (for example, <c>Inbox/Projects</c>).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Folder display name.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Parent folder id, when available.</summary>
        public string? ParentId { get; set; }

        /// <summary>Graph well-known folder name, when available.</summary>
        public string? WellKnownName { get; set; }

        /// <summary>Total item count, when available.</summary>
        public int? TotalItemCount { get; set; }

        /// <summary>Unread item count, when available.</summary>
        public int? UnreadItemCount { get; set; }
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
    /// Graph mailbox conversation list result.
    /// </summary>
    public sealed class GraphMailboxConversationListResult {
        /// <summary>Graph conversation id used for listing.</summary>
        public string ConversationId { get; set; } = string.Empty;

        /// <summary>Total number of messages available in the conversation slice source.</summary>
        public int TotalCount { get; set; }

        /// <summary>Paged conversation message summaries.</summary>
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
    /// Graph mailbox import result.
    /// </summary>
    public sealed class GraphMailboxImportResult {
        /// <summary>Resolved Graph folder selector used for import.</summary>
        public string FolderSelector { get; set; } = string.Empty;

        /// <summary>Created Graph native message id.</summary>
        public string? NativeId { get; set; }

        /// <summary>Normalized RFC822 Message-Id used for import.</summary>
        public string? MessageId { get; set; }
    }

    /// <summary>
    /// Graph mailbox send result.
    /// </summary>
    public sealed class GraphMailboxSendResult {
        /// <summary>Graph draft id that was sent.</summary>
        public string? DraftId { get; set; }

        /// <summary>Normalized RFC822 Message-Id used for send.</summary>
        public string? MessageId { get; set; }
    }

    /// <summary>
    /// Graph mailbox duplicate probe result.
    /// </summary>
    public sealed class GraphMailboxDuplicateProbeResult {
        /// <summary>True when a matching message was found.</summary>
        public bool IsMatch { get; set; }

        /// <summary>Resolved Graph folder selector used for probing.</summary>
        public string? FolderSelector { get; set; }

        /// <summary>Matched Graph native message id, when available.</summary>
        public string? NativeId { get; set; }

        /// <summary>Matched normalized RFC822 Message-Id.</summary>
        public string? MessageId { get; set; }
    }

    /// <summary>
    /// Graph mailbox webhook subscription result.
    /// </summary>
    public sealed class GraphMailboxSubscriptionResult {
        /// <summary>Graph subscription id.</summary>
        public string? SubscriptionId { get; set; }

        /// <summary>Subscription resource path.</summary>
        public string? Resource { get; set; }

        /// <summary>Client-state value when provided.</summary>
        public string? ClientState { get; set; }

        /// <summary>Subscription expiration value.</summary>
        public DateTimeOffset ExpirationDateTime { get; set; }
    }

    /// <summary>
    /// Graph mailbox webhook delete result.
    /// </summary>
    public sealed class GraphMailboxSubscriptionDeleteResult {
        /// <summary>True when delete operation succeeded.</summary>
        public bool Deleted { get; set; }

        /// <summary>True when delete succeeded because subscription was already gone.</summary>
        public bool AlreadyDeleted { get; set; }
    }

    /// <summary>
    /// Graph mailbox webhook renew result.
    /// </summary>
    public sealed class GraphMailboxSubscriptionRenewResult {
        /// <summary>True when renew operation succeeded.</summary>
        public bool Renewed { get; set; }

        /// <summary>True when renew failed because subscription was already missing.</summary>
        public bool Missing { get; set; }

        /// <summary>Renewed subscription payload when available.</summary>
        public GraphMailboxSubscriptionResult? Subscription { get; set; }
    }

    /// <summary>
    /// Graph mailbox threading metadata.
    /// </summary>
    public sealed class GraphMailboxThreadingMetadataResult {
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
