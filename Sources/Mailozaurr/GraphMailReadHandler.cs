using MimeKit;
using System.Globalization;

namespace Mailozaurr;

/// <summary>
/// Normalized Graph read handler backed by Mailozaurr Graph helpers.
/// </summary>
public sealed class GraphMailReadHandler : IMailReadHandler {
    private const string SummarySelect = "id,subject,receivedDateTime,from,toRecipients,internetMessageId,hasAttachments,isRead,flag,conversationId";
    private readonly IGraphSessionFactory _sessionFactory;
    private readonly Func<GraphSession, MailProfile, MailFolderQuery, CancellationToken, Task<IReadOnlyList<FolderRef>>> _getFoldersAsync;
    private readonly Func<GraphSession, MailProfile, MailSearchRequest, CancellationToken, Task<IReadOnlyList<MessageSummary>>> _searchAsync;
    private readonly Func<GraphSession, MailProfile, GetMessageRequest, CancellationToken, Task<MessageDetail?>> _getMessageAsync;
    private readonly Func<GraphSession, MailProfile, SaveAttachmentRequest, CancellationToken, Task<OperationResult>> _saveAttachmentAsync;

    /// <summary>
    /// Creates a new Graph read handler.
    /// </summary>
    public GraphMailReadHandler(
        IGraphSessionFactory sessionFactory,
        Func<GraphSession, MailProfile, MailFolderQuery, CancellationToken, Task<IReadOnlyList<FolderRef>>>? getFoldersAsync = null,
        Func<GraphSession, MailProfile, MailSearchRequest, CancellationToken, Task<IReadOnlyList<MessageSummary>>>? searchAsync = null,
        Func<GraphSession, MailProfile, GetMessageRequest, CancellationToken, Task<MessageDetail?>>? getMessageAsync = null,
        Func<GraphSession, MailProfile, SaveAttachmentRequest, CancellationToken, Task<OperationResult>>? saveAttachmentAsync = null) {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _getFoldersAsync = getFoldersAsync ?? DefaultGetFoldersAsync;
        _searchAsync = searchAsync ?? DefaultSearchAsync;
        _getMessageAsync = getMessageAsync ?? DefaultGetMessageAsync;
        _saveAttachmentAsync = saveAttachmentAsync ?? DefaultSaveAttachmentAsync;
    }

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.Graph;

    /// <inheritdoc />
    public async Task<IReadOnlyList<FolderRef>> GetFoldersAsync(MailProfile profile, MailFolderQuery query, CancellationToken cancellationToken = default) {
        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _getFoldersAsync(session, profile, query, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MessageSummary>> SearchAsync(MailProfile profile, MailSearchRequest request, CancellationToken cancellationToken = default) {
        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _searchAsync(session, profile, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MessageDetail?> GetMessageAsync(MailProfile profile, GetMessageRequest request, CancellationToken cancellationToken = default) {
        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _getMessageAsync(session, profile, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> SaveAttachmentAsync(MailProfile profile, SaveAttachmentRequest request, CancellationToken cancellationToken = default) {
        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _saveAttachmentAsync(session, profile, request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<FolderRef>> DefaultGetFoldersAsync(
        GraphSession session,
        MailProfile profile,
        MailFolderQuery query,
        CancellationToken cancellationToken) {
        var userId = ResolveUserId(profile, query.MailboxId);
        var folders = await session.Client.ListMailFoldersRecursiveAsync(userId, cancellationToken: cancellationToken).ConfigureAwait(false);
        var nodes = folders.ToDictionary(
            folder => folder.Id,
            folder => new GraphFolderNode(
                folder.Id,
                folder.DisplayName,
                folder.ParentFolderId,
                folder.WellKnownName,
                folder.TotalItemCount,
                folder.UnreadItemCount),
            StringComparer.Ordinal);

        string BuildPath(string id) {
            if (!nodes.TryGetValue(id, out var node)) {
                return id;
            }

            var parts = new List<string>();
            var current = node;
            var guard = 0;
            while (guard++ < 100) {
                parts.Add(current.DisplayName);
                if (string.IsNullOrWhiteSpace(current.ParentId) ||
                    !nodes.TryGetValue(current.ParentId!, out current)) {
                    break;
                }
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        bool MatchesFilter(GraphFolderNode node) {
            if (query.RootOnly) {
                return string.IsNullOrWhiteSpace(node.ParentId);
            }
            if (string.IsNullOrWhiteSpace(query.ParentFolderId)) {
                return true;
            }

            if (string.Equals(node.Id, query.ParentFolderId, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            var currentParentId = node.ParentId;
            var guard = 0;
            while (!string.IsNullOrWhiteSpace(currentParentId) && guard++ < 100) {
                if (string.Equals(currentParentId, query.ParentFolderId, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }

                if (!nodes.TryGetValue(currentParentId!, out var parentNode)) {
                    break;
                }

                currentParentId = parentNode.ParentId;
            }

            return false;
        }

        return nodes.Values
            .Where(MatchesFilter)
            .Select(node => new FolderRef {
                ProfileId = profile.Id,
                MailboxId = userId,
                Id = node.Id,
                DisplayName = node.DisplayName,
                Path = BuildPath(node.Id),
                SpecialUse = node.WellKnownName,
                MessageCount = node.TotalItemCount,
                UnreadCount = node.UnreadItemCount
            })
            .OrderBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<IReadOnlyList<MessageSummary>> DefaultSearchAsync(
        GraphSession session,
        MailProfile profile,
        MailSearchRequest request,
        CancellationToken cancellationToken) {
        var userId = ResolveUserId(profile, request.MailboxId);
        var folder = ResolveFolder(request.FolderId, profile);
        var filterParts = new List<string>();

        if (request.IsRead.HasValue) {
            filterParts.Add("isRead eq " + (request.IsRead.Value ? "true" : "false"));
        }
        if (request.HasAttachments) {
            filterParts.Add("hasAttachments eq true");
        }
        if (request.Since.HasValue) {
            filterParts.Add("receivedDateTime ge " + request.Since.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
        }
        if (request.Before.HasValue) {
            filterParts.Add("receivedDateTime lt " + request.Before.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
        }

        var searchTerms = new List<string>();
        AddSearchToken(searchTerms, request.QueryText);
        AddSearchToken(searchTerms, request.SubjectContains);
        AddSearchToken(searchTerms, request.FromContains);
        AddSearchToken(searchTerms, request.ToContains);

        var page = await session.Client.ListMessagesAsync(
            folder,
            userId: userId,
            top: request.Limit ?? 100,
            skip: null,
            select: SummarySelect,
            orderBy: "receivedDateTime desc",
            filter: filterParts.Count == 0 ? null : string.Join(" and ", filterParts),
            search: searchTerms.Count == 0 ? null : string.Join(" ", searchTerms),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return page.Items
            .Select(message => MapSummary(profile.Id, folder, message))
            .ToArray();
    }

    private static async Task<MessageDetail?> DefaultGetMessageAsync(
        GraphSession session,
        MailProfile profile,
        GetMessageRequest request,
        CancellationToken cancellationToken) {
        var userId = ResolveUserId(profile, request.MailboxId);
        var folder = ResolveFolder(request.FolderId, profile);
        var maxMimeBytes = GetMaxMimeBytes(profile);
        var metadata = await session.Client.GetMessageAsync(
            request.MessageId,
            userId: userId,
            select: SummarySelect,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var mimeBytes = await session.Client.GetMessageMimeAsync(
            request.MessageId,
            userId: userId,
            maxBytes: maxMimeBytes,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        MimeMessage message;
        try {
            message = MimeMessage.Load(new MemoryStream(mimeBytes, writable: false));
        } catch (Exception ex) {
            throw new InvalidDataException("Failed to parse Graph MIME message.", ex);
        }

        var summary = MapSummary(profile.Id, folder, metadata, message);
        var detail = new MessageDetail {
            ProfileId = profile.Id,
            Id = summary.Id,
            Summary = summary,
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
            Attachments = message.Attachments.Select((attachment, index) => new AttachmentSummary {
                MessageId = summary.Id,
                Id = index.ToString(CultureInfo.InvariantCulture),
                FileName = MimeAttachmentStorage.GetAttachmentFileName(
                    attachment,
                    MimeAttachmentStorage.CreateStorageIdentity(profile.Id, summary.Id, index.ToString(CultureInfo.InvariantCulture))),
                ContentType = attachment.ContentType?.MimeType
            }).ToList()
        };

        if (request.IncludeRawContent) {
            using var stream = new MemoryStream(mimeBytes, writable: false);
            using var reader = new StreamReader(stream);
            detail.RawContent = reader.ReadToEnd();
        }

        return detail;
    }

    private static async Task<OperationResult> DefaultSaveAttachmentAsync(
        GraphSession session,
        MailProfile profile,
        SaveAttachmentRequest request,
        CancellationToken cancellationToken) {
        var userId = ResolveUserId(profile, request.MailboxId);
        var maxMimeBytes = GetMaxMimeBytes(profile);
        var mimeBytes = await session.Client.GetMessageMimeAsync(
            request.MessageId,
            userId: userId,
            maxBytes: maxMimeBytes,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        MimeMessage message;
        try {
            message = MimeMessage.Load(new MemoryStream(mimeBytes, writable: false));
        } catch (Exception ex) {
            throw new InvalidDataException("Failed to parse Graph MIME message.", ex);
        }

        var attachments = message.Attachments.ToList();
        if (attachments.Count == 0) {
            return OperationResult.Failure("attachment_not_found", "Message has no attachments.");
        }

        var attachmentIndex = MimeAttachmentStorage.ResolveAttachmentIndex(
            attachments,
            request.AttachmentId,
            index => MimeAttachmentStorage.CreateStorageIdentity(
                profile.Id,
                request.MessageId,
                index.ToString(CultureInfo.InvariantCulture)));
        if (attachmentIndex < 0) {
            return OperationResult.Failure("attachment_not_found", $"Attachment '{request.AttachmentId}' was not found.");
        }
        var attachment = attachments[attachmentIndex];

        var destinationPath = MimeAttachmentStorage.ResolveDestinationPath(
            request.DestinationPath,
            attachment,
            MimeAttachmentStorage.CreateStorageIdentity(
                profile.Id,
                profile.Kind.ToString(),
                CanonicalizeUserIdForStorage(profile, userId),
                request.MessageId,
                attachmentIndex.ToString(CultureInfo.InvariantCulture)));
        if (File.Exists(destinationPath) && !request.Overwrite) {
            return OperationResult.Failure("destination_exists", $"Destination '{destinationPath}' already exists.");
        }

        MimeAttachmentStorage.SaveAttachment(attachment, destinationPath);
        return OperationResult.Success($"Attachment saved to '{destinationPath}'.");
    }

    private static MessageSummary MapSummary(
        string profileId,
        string folderId,
        GraphMailMessage message,
        MimeMessage? mimeMessage = null) => new() {
            ProfileId = profileId,
            Id = message.Id,
            ThreadId = message.ConversationId,
            FolderId = folderId,
            Subject = string.IsNullOrWhiteSpace(message.Subject) ? mimeMessage?.Subject : message.Subject,
            Preview = mimeMessage?.TextBody,
            From = MapRecipients(message.From, mimeMessage?.From.Mailboxes),
            To = MapRecipients(message.ToRecipients, mimeMessage?.To.Mailboxes),
            ReceivedAt = message.ReceivedDateTime ?? mimeMessage?.Date,
            SentAt = mimeMessage?.Date,
            IsRead = message.IsRead,
            HasAttachments = message.HasAttachments ?? mimeMessage?.Attachments.Any() ?? false
        };

    private static List<MessageRecipient> MapRecipients(GraphEmailAddress? sender, IEnumerable<MailboxAddress>? fallback) {
        if (sender?.Email != null && !string.IsNullOrWhiteSpace(sender.Email.Address)) {
            return new List<MessageRecipient> {
                new() {
                    Name = sender.Email.Name,
                    Address = sender.Email.Address
                }
            };
        }

        return fallback?
            .Select(mailbox => new MessageRecipient {
                Name = mailbox.Name,
                Address = mailbox.Address ?? string.Empty
            })
            .ToList() ?? new List<MessageRecipient>();
    }

    private static List<MessageRecipient> MapRecipients(IEnumerable<GraphEmailAddress>? recipients, IEnumerable<MailboxAddress>? fallback) {
        var output = recipients?
            .Where(recipient => recipient.Email != null && !string.IsNullOrWhiteSpace(recipient.Email.Address))
            .Select(recipient => new MessageRecipient {
                Name = recipient.Email.Name,
                Address = recipient.Email.Address
            })
            .ToList();
        if (output != null && output.Count > 0) {
            return output;
        }

        return fallback?
            .Select(mailbox => new MessageRecipient {
                Name = mailbox.Name,
                Address = mailbox.Address ?? string.Empty
            })
            .ToList() ?? new List<MessageRecipient>();
    }

    private static string ResolveFolder(string? folderId, MailProfile profile) {
        if (!string.IsNullOrWhiteSpace(folderId)) {
            return folderId!.Trim();
        }
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.Folder, out var folder) && !string.IsNullOrWhiteSpace(folder)) {
            return folder!.Trim();
        }

        return "inbox";
    }

    internal static string ResolveUserId(MailProfile profile, string? mailboxOverride = null) {
        if (!string.IsNullOrWhiteSpace(mailboxOverride)) {
            return mailboxOverride!.Trim();
        }
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var mailbox) && !string.IsNullOrWhiteSpace(mailbox)) {
            return mailbox!.Trim();
        }
        if (!string.IsNullOrWhiteSpace(profile.DefaultMailbox)) {
            return profile.DefaultMailbox!.Trim();
        }

        return "me";
    }

    internal static string CanonicalizeUserIdForStorage(string? userId) {
        var normalized = string.IsNullOrWhiteSpace(userId) ? "me" : userId!.Trim();
        return string.Equals(normalized, "me", StringComparison.OrdinalIgnoreCase)
            ? "me"
            : normalized.ToLowerInvariant();
    }

    internal static string CanonicalizeUserIdForStorage(MailProfile profile, string? userId) {
        if (profile == null) throw new ArgumentNullException(nameof(profile));

        var normalized = CanonicalizeUserIdForStorage(userId);
        if (normalized == "me" || IsConfiguredMailbox(profile, normalized)) {
            return "me";
        }

        return normalized;
    }

    private static bool IsConfiguredMailbox(MailProfile profile, string userId) {
        if (!string.IsNullOrWhiteSpace(profile.DefaultMailbox) &&
            string.Equals(profile.DefaultMailbox!.Trim(), userId, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var mailbox) &&
               !string.IsNullOrWhiteSpace(mailbox) &&
               string.Equals(mailbox!.Trim(), userId, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetMaxMimeBytes(MailProfile profile) {
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.MaxBodyBytes, out var raw) &&
            int.TryParse(raw, out var value) &&
            value > 0) {
            return value;
        }

        return GraphMailboxBrowser.DefaultMaxMimeBytes;
    }

    private static void AddSearchToken(List<string> tokens, string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return;
        }

        var normalized = value!.Trim();
        if (normalized.Length == 0) {
            return;
        }

        tokens.Add(normalized.Replace("\"", string.Empty));
    }

    private sealed class GraphFolderNode {
        public GraphFolderNode(
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
}
