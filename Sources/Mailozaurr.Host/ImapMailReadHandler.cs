using MailKit;
using MailKit.Net.Imap;
using MimeKit;

namespace Mailozaurr.Hosting;

/// <summary>
/// Normalized IMAP read handler backed by Mailozaurr IMAP helpers.
/// </summary>
public sealed class ImapMailReadHandler : IMailReadHandler {
    private readonly IImapSessionFactory _sessionFactory;
    private readonly Func<ImapClient, MailFolderQuery, CancellationToken, Task<IReadOnlyList<FolderRef>>> _getFoldersAsync;
    private readonly Func<ImapClient, MailProfile, MailSearchRequest, CancellationToken, Task<IReadOnlyList<MessageSummary>>> _searchAsync;
    private readonly Func<ImapClient, MailProfile, GetMessageRequest, CancellationToken, Task<MessageDetail?>> _getMessageAsync;
    private readonly Func<ImapClient, MailProfile, SaveAttachmentRequest, CancellationToken, Task<OperationResult>> _saveAttachmentAsync;

    /// <summary>
    /// Creates a new IMAP read handler.
    /// </summary>
    public ImapMailReadHandler(
        IImapSessionFactory sessionFactory,
        Func<ImapClient, MailFolderQuery, CancellationToken, Task<IReadOnlyList<FolderRef>>>? getFoldersAsync = null,
        Func<ImapClient, MailProfile, MailSearchRequest, CancellationToken, Task<IReadOnlyList<MessageSummary>>>? searchAsync = null,
        Func<ImapClient, MailProfile, GetMessageRequest, CancellationToken, Task<MessageDetail?>>? getMessageAsync = null,
        Func<ImapClient, MailProfile, SaveAttachmentRequest, CancellationToken, Task<OperationResult>>? saveAttachmentAsync = null) {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _getFoldersAsync = getFoldersAsync ?? DefaultGetFoldersAsync;
        _searchAsync = searchAsync ?? DefaultSearchAsync;
        _getMessageAsync = getMessageAsync ?? DefaultGetMessageAsync;
        _saveAttachmentAsync = saveAttachmentAsync ?? DefaultSaveAttachmentAsync;
    }

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.Imap;

    /// <inheritdoc />
    public async Task<IReadOnlyList<FolderRef>> GetFoldersAsync(MailProfile profile, MailFolderQuery query, CancellationToken cancellationToken = default) {
        using var client = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _getFoldersAsync(client, query, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MessageSummary>> SearchAsync(MailProfile profile, MailSearchRequest request, CancellationToken cancellationToken = default) {
        using var client = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _searchAsync(client, profile, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MessageDetail?> GetMessageAsync(MailProfile profile, GetMessageRequest request, CancellationToken cancellationToken = default) {
        using var client = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _getMessageAsync(client, profile, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> SaveAttachmentAsync(MailProfile profile, SaveAttachmentRequest request, CancellationToken cancellationToken = default) {
        using var client = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _saveAttachmentAsync(client, profile, request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<FolderRef>> DefaultGetFoldersAsync(
        ImapClient client,
        MailFolderQuery query,
        CancellationToken cancellationToken) {
        var results = new List<FolderRef>();
        if (query.RootOnly) {
            await foreach (var folder in ImapRootFolderEnumerator.EnumerateAsync(client, cancellationToken).ConfigureAwait(false)) {
                results.Add(MapFolder(folder, query.ProfileId));
            }
            return results;
        }

        var root = client.PersonalNamespaces.Count > 0
            ? client.GetFolder(client.PersonalNamespaces[0])
            : client.GetFolder(string.Empty);
        await CollectFoldersAsync(root, results, query.ProfileId, query.ParentFolderId, cancellationToken).ConfigureAwait(false);
        return results;
    }

    private static async Task CollectFoldersAsync(
        IMailFolder root,
        List<FolderRef> results,
        string profileId,
        string? parentFolderId,
        CancellationToken cancellationToken) {
        var folders = await root.GetSubfoldersAsync(false, cancellationToken).ConfigureAwait(false);
        foreach (var folder in folders) {
            if (!string.IsNullOrWhiteSpace(parentFolderId) &&
                !string.Equals(folder.ParentFolder?.FullName, parentFolderId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(folder.FullName, parentFolderId, StringComparison.OrdinalIgnoreCase)) {
                if (folder.Attributes.HasFlag(FolderAttributes.HasChildren)) {
                    await CollectFoldersAsync(folder, results, profileId, parentFolderId, cancellationToken).ConfigureAwait(false);
                }
                continue;
            }

            results.Add(MapFolder(folder, profileId));
            if (folder.Attributes.HasFlag(FolderAttributes.HasChildren)) {
                await CollectFoldersAsync(folder, results, profileId, null, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static FolderRef MapFolder(IMailFolder folder, string profileId) => new() {
        ProfileId = profileId,
        Id = folder.FullName,
        DisplayName = folder.Name,
        Path = folder.FullName,
        MailboxId = null,
        SpecialUse = folder.Attributes.HasFlag(FolderAttributes.Inbox) ? "Inbox" : null,
        MessageCount = folder.IsOpen ? folder.Count : (int?)null,
        UnreadCount = folder.IsOpen ? folder.Unread : (int?)null
    };

    private static async Task<IReadOnlyList<MessageSummary>> DefaultSearchAsync(
        ImapClient client,
        MailProfile profile,
        MailSearchRequest request,
        CancellationToken cancellationToken) {
        var messages = await MailboxSearcher.SearchImapAsync(
            client,
            folder: ResolveFolder(request.FolderId, profile),
            subject: request.SubjectContains,
            fromContains: request.FromContains,
            toContains: request.ToContains,
            hasAttachment: request.HasAttachments,
            since: request.Since?.UtcDateTime,
            before: request.Before?.UtcDateTime,
            maxResults: request.Limit ?? 0,
            cancellationToken: cancellationToken,
            queryString: request.QueryText).ConfigureAwait(false);

        return messages.Select(message => MapSummary(profile.Id, ResolveFolder(request.FolderId, profile), message)).ToArray();
    }

    private static async Task<MessageDetail?> DefaultGetMessageAsync(
        ImapClient client,
        MailProfile profile,
        GetMessageRequest request,
        CancellationToken cancellationToken) {
        var folder = ResolveFolder(request.FolderId, profile);
        var uid = ParseUid(request.MessageId);
        var maxBodyBytes = GetMaxBodyBytes(profile);
        var result = await ImapMessageReader.ReadAsync(
            client,
            new ImapMessageReadRequest(uid, folder, maxBodyBytes),
            cancellationToken).ConfigureAwait(false);

        var summary = new MessageSummary {
            ProfileId = profile.Id,
            Id = result.Uid.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FolderId = result.Folder,
            Subject = result.Subject,
            From = SplitAddresses(result.From),
            To = SplitAddresses(result.To),
            ReceivedAt = result.DateUtc,
            HasAttachments = result.HasAttachments
        };

        var detail = new MessageDetail {
            ProfileId = profile.Id,
            Id = summary.Id,
            Summary = summary,
            TextBody = result.TextBody,
            HtmlBody = result.HtmlBody,
            Attachments = result.Attachments.Select((attachment, index) => new AttachmentSummary {
                MessageId = summary.Id,
                Id = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                FileName = attachment.FileName,
                ContentType = attachment.ContentType
            }).ToList()
        };

        if (request.IncludeRawContent) {
            var mailFolder = client.GetCachedFolder(folder, FolderAccess.ReadOnly);
            var message = await mailFolder.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
            using var stream = new MemoryStream();
            message.WriteTo(stream);
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            detail.RawContent = reader.ReadToEnd();
        }

        return detail;
    }

    private static async Task<OperationResult> DefaultSaveAttachmentAsync(
        ImapClient client,
        MailProfile profile,
        SaveAttachmentRequest request,
        CancellationToken cancellationToken) {
        var folder = ResolveFolder(request.FolderId, profile);
        var uid = ParseUid(request.MessageId);
        var mailFolder = client.GetCachedFolder(folder, FolderAccess.ReadOnly);
        var message = await mailFolder.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
        var attachments = message.Attachments.ToList();
        if (attachments.Count == 0) {
            return OperationResult.Failure("attachment_not_found", "Message has no attachments.");
        }

        var attachment = ResolveAttachment(attachments, request.AttachmentId);
        if (attachment == null) {
            return OperationResult.Failure("attachment_not_found", $"Attachment '{request.AttachmentId}' was not found.");
        }

        var destinationPath = MimeAttachmentStorage.ResolveDestinationPath(request.DestinationPath, attachment);
        if (File.Exists(destinationPath) && !request.Overwrite) {
            return OperationResult.Failure("destination_exists", $"Destination '{destinationPath}' already exists.");
        }

        MimeAttachmentStorage.SaveAttachment(attachment, destinationPath);
        return OperationResult.Success($"Attachment saved to '{destinationPath}'.");
    }

    private static MessageSummary MapSummary(string profileId, string folder, ImapEmailMessage message) => new() {
        ProfileId = profileId,
        Id = message.Uid.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        FolderId = folder,
        Subject = message.Message.Subject,
        Preview = message.Message.TextBody,
        From = message.Message.From.Mailboxes.Select(mailbox => new MessageRecipient {
            Name = mailbox.Name,
            Address = mailbox.Address ?? string.Empty
        }).ToList(),
        To = message.Message.To.Mailboxes.Select(mailbox => new MessageRecipient {
            Name = mailbox.Name,
            Address = mailbox.Address ?? string.Empty
        }).ToList(),
        SentAt = message.Message.Date,
        ReceivedAt = message.Message.Date,
        HasAttachments = message.Message.Attachments.Any(),
        Priority = message.Message.Priority switch {
            MimeKit.MessagePriority.Urgent => MessagePriority.High,
            MimeKit.MessagePriority.NonUrgent => MessagePriority.Low,
            _ => MessagePriority.Normal
        }
    };

    private static UniqueId ParseUid(string value) {
        if (uint.TryParse(value, out var uid)) {
            return new UniqueId(uid);
        }

        throw new InvalidOperationException($"Message id '{value}' is not a valid IMAP UID.");
    }

    private static string ResolveFolder(string? folderId, MailProfile profile) {
        if (!string.IsNullOrWhiteSpace(folderId)) {
            return folderId!.Trim();
        }
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.Folder, out var folder) && !string.IsNullOrWhiteSpace(folder)) {
            return folder!.Trim();
        }
        return "INBOX";
    }

    private static long GetMaxBodyBytes(MailProfile profile) {
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.MaxBodyBytes, out var raw) &&
            long.TryParse(raw, out var value) &&
            value > 0) {
            return value;
        }

        return 256 * 1024;
    }

    private static List<MessageRecipient> SplitAddresses(string? addresses) {
        if (string.IsNullOrWhiteSpace(addresses)) {
            return new List<MessageRecipient>();
        }

        var normalized = addresses!;
        return normalized
            .Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
            .Select(address => new MessageRecipient { Address = address })
            .ToList();
    }

    private static MimeEntity? ResolveAttachment(IReadOnlyList<MimeEntity> attachments, string attachmentId) =>
        MimeAttachmentStorage.ResolveAttachment(attachments, attachmentId);
}