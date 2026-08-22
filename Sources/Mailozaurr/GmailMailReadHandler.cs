using MimeKit;
using System.Globalization;

namespace Mailozaurr;

/// <summary>
/// Normalized Gmail read handler backed by Mailozaurr Gmail helpers.
/// </summary>
public sealed class GmailMailReadHandler : IMailReadHandler {
    private readonly IGmailSessionFactory _sessionFactory;
    private readonly Func<GmailSession, MailProfile, MailFolderQuery, CancellationToken, Task<IReadOnlyList<FolderRef>>> _getFoldersAsync;
    private readonly Func<GmailSession, MailProfile, MailSearchRequest, CancellationToken, Task<IReadOnlyList<MessageSummary>>> _searchAsync;
    private readonly Func<GmailSession, MailProfile, GetMessageRequest, CancellationToken, Task<MessageDetail?>> _getMessageAsync;
    private readonly Func<GmailSession, MailProfile, SaveAttachmentRequest, CancellationToken, Task<OperationResult>> _saveAttachmentAsync;

    /// <summary>
    /// Creates a new Gmail read handler.
    /// </summary>
    public GmailMailReadHandler(
        IGmailSessionFactory sessionFactory,
        Func<GmailSession, MailProfile, MailFolderQuery, CancellationToken, Task<IReadOnlyList<FolderRef>>>? getFoldersAsync = null,
        Func<GmailSession, MailProfile, MailSearchRequest, CancellationToken, Task<IReadOnlyList<MessageSummary>>>? searchAsync = null,
        Func<GmailSession, MailProfile, GetMessageRequest, CancellationToken, Task<MessageDetail?>>? getMessageAsync = null,
        Func<GmailSession, MailProfile, SaveAttachmentRequest, CancellationToken, Task<OperationResult>>? saveAttachmentAsync = null) {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _getFoldersAsync = getFoldersAsync ?? DefaultGetFoldersAsync;
        _searchAsync = searchAsync ?? DefaultSearchAsync;
        _getMessageAsync = getMessageAsync ?? DefaultGetMessageAsync;
        _saveAttachmentAsync = saveAttachmentAsync ?? DefaultSaveAttachmentAsync;
    }

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.Gmail;

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
        GmailSession session,
        MailProfile profile,
        MailFolderQuery query,
        CancellationToken cancellationToken) {
        var userId = ResolveUserId(profile, query.MailboxId);
        var folders = await session.Browser.ListFoldersAsync(cancellationToken).ConfigureAwait(false);

        bool MatchesFilter(GmailMailboxBrowser.GmailMailboxFolderSummary folder) {
            if (query.RootOnly) {
                return folder.Name.IndexOf("/", StringComparison.Ordinal) < 0;
            }
            if (string.IsNullOrWhiteSpace(query.ParentFolderId)) {
                return true;
            }

            var parent = GetParentPath(folder.Name);
            return string.Equals(parent, query.ParentFolderId, StringComparison.OrdinalIgnoreCase);
        }

        return folders
            .Where(MatchesFilter)
            .Select(folder => new FolderRef {
                ProfileId = profile.Id,
                MailboxId = userId,
                Id = folder.Id,
                DisplayName = folder.Name,
                Path = folder.Name,
                SpecialUse = folder.Type
            })
            .OrderBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<IReadOnlyList<MessageSummary>> DefaultSearchAsync(
        GmailSession session,
        MailProfile profile,
        MailSearchRequest request,
        CancellationToken cancellationToken) {
        var folder = ResolveFolder(request.FolderId, profile);
        var result = await session.Browser.SearchMessagesAsync(
            new GmailMailboxBrowser.GmailMailboxSearchRequest {
                Folder = folder,
                Query = request.QueryText,
                SubjectContains = request.SubjectContains,
                FromContains = request.FromContains,
                ToContains = request.ToContains,
                UnseenOnly = request.IsRead.HasValue ? !request.IsRead.Value : false,
                HasAttachment = request.HasAttachments,
                SinceUtc = request.Since?.UtcDateTime,
                BeforeUtc = request.Before?.UtcDateTime
            },
            request.Limit ?? 100,
            cancellationToken).ConfigureAwait(false);

        return result.Messages
            .Select(message => MapSummary(profile.Id, folder, message))
            .ToArray();
    }

    private static async Task<MessageDetail?> DefaultGetMessageAsync(
        GmailSession session,
        MailProfile profile,
        GetMessageRequest request,
        CancellationToken cancellationToken) {
        var folder = ResolveFolder(request.FolderId, profile);
        var messageId = request.MessageId.Trim();
        var getResult = await session.Browser.GetMessageContentAsync(
            messageId,
            GetMaxMimeBytes(profile),
            cancellationToken).ConfigureAwait(false);
        var summary = MapSummary(
            profile.Id,
            folder,
            await session.Browser.GetMessageSummaryAsync(messageId, cancellationToken).ConfigureAwait(false),
            getResult.Message);

        var detail = new MessageDetail {
            ProfileId = profile.Id,
            Id = summary.Id,
            Summary = summary,
            TextBody = getResult.Message.TextBody,
            HtmlBody = getResult.Message.HtmlBody,
            Attachments = (await session.Client.ListAttachmentsAsync(session.UserId, messageId, cancellationToken).ConfigureAwait(false))
                .OfType<GmailAttachmentInfo>()
                .Select((attachment, index) => new AttachmentSummary {
                    MessageId = summary.Id,
                    Id = !string.IsNullOrWhiteSpace(attachment.Id)
                        ? attachment.Id!.Trim()
                        : index.ToString(CultureInfo.InvariantCulture),
                    FileName = string.IsNullOrWhiteSpace(attachment.FileName)
                        ? (!string.IsNullOrWhiteSpace(attachment.Id)
                            ? attachment.Id!.Trim()
                            : index.ToString(CultureInfo.InvariantCulture))
                        : attachment.FileName!.Trim(),
                    ContentType = attachment.MimeType
                })
                .ToList()
        };

        if (request.IncludeRawContent) {
            using var stream = new MemoryStream();
            await getResult.Message.WriteToAsync(stream, cancellationToken).ConfigureAwait(false);
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            detail.RawContent = reader.ReadToEnd();
        }

        return detail;
    }

    private static async Task<OperationResult> DefaultSaveAttachmentAsync(
        GmailSession session,
        MailProfile profile,
        SaveAttachmentRequest request,
        CancellationToken cancellationToken) {
        var attachments = await session.Client.ListAttachmentsAsync(session.UserId, request.MessageId, cancellationToken).ConfigureAwait(false);
        if (attachments == null || attachments.Count == 0) {
            return OperationResult.Failure("attachment_not_found", "Message has no attachments.");
        }

        var resolved = ResolveAttachment(attachments, request.AttachmentId);
        if (resolved == null || string.IsNullOrWhiteSpace(resolved.Id)) {
            return OperationResult.Failure("attachment_not_found", $"Attachment '{request.AttachmentId}' was not found.");
        }

        var fileName = string.IsNullOrWhiteSpace(resolved.FileName) ? resolved.Id!.Trim() : resolved.FileName!.Trim();
        var destinationPath = MimeAttachmentStorage.ResolveDestinationPath(
            request.DestinationPath,
            fileName,
            MimeAttachmentStorage.CreateStorageIdentity(
                profile.Id,
                profile.Kind.ToString(),
                session.UserId,
                request.MessageId,
                resolved.Id!.Trim()));
        if (File.Exists(destinationPath) && !request.Overwrite) {
            return OperationResult.Failure("destination_exists", $"Destination '{destinationPath}' already exists.");
        }

        var bytes = await session.Client.DownloadAttachmentAsync(
            session.UserId,
            request.MessageId,
            resolved.Id!.Trim(),
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        using (var stream = File.Create(destinationPath)) {
#if NET8_0_OR_GREATER
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
#else
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
#endif
        }
        return OperationResult.Success($"Attachment saved to '{destinationPath}'.");
    }

    private static MessageSummary MapSummary(
        string profileId,
        string folderId,
        GmailMailboxBrowser.GmailMailboxMessageSummary message,
        MimeMessage? mimeMessage = null) => new() {
            ProfileId = profileId,
            Id = message.NativeId,
            ThreadId = message.NativeThreadId,
            FolderId = folderId,
            Subject = message.Subject ?? mimeMessage?.Subject,
            Preview = mimeMessage?.TextBody,
            From = MapRecipients(message.From, mimeMessage?.From.Mailboxes),
            To = MapRecipients(message.To, mimeMessage?.To.Mailboxes),
            ReceivedAt = message.DateUtc,
            SentAt = mimeMessage?.Date,
            IsRead = message.Seen,
            HasAttachments = message.HasAttachments
        };

    private static List<MessageRecipient> MapRecipients(string? raw, IEnumerable<MailboxAddress>? fallback) {
        if (!string.IsNullOrWhiteSpace(raw)) {
            var normalizedRaw = raw!.Trim();
            try {
                var parsed = InternetAddressList.Parse(normalizedRaw);
                var recipients = parsed.Mailboxes.Select(mailbox => new MessageRecipient {
                    Name = mailbox.Name,
                    Address = mailbox.Address ?? string.Empty
                }).ToList();
                if (recipients.Count > 0) {
                    return recipients;
                }
            } catch {
                return new List<MessageRecipient> {
                    new() {
                        Address = normalizedRaw
                    }
                };
            }
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

        return "INBOX";
    }

    private static string ResolveUserId(MailProfile profile, string? mailboxOverride = null) {
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

    private static int GetMaxMimeBytes(MailProfile profile) {
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.MaxBodyBytes, out var raw) &&
            int.TryParse(raw, out var value) &&
            value > 0) {
            return value;
        }

        return GmailMailboxBrowser.DefaultMaxMimeBytes;
    }

    private static GmailAttachmentInfo? ResolveAttachment(IList<GmailAttachmentInfo> attachments, string attachmentId) {
        if (int.TryParse(attachmentId, out var index) && index >= 0 && index < attachments.Count) {
            return attachments[index];
        }

        return attachments.FirstOrDefault(attachment =>
            string.Equals(attachment.Id, attachmentId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(attachment.FileName, attachmentId, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetParentPath(string path) {
        var separatorIndex = path.LastIndexOf('/');
        if (separatorIndex <= 0) {
            return null;
        }

        return path.Substring(0, separatorIndex);
    }
}
