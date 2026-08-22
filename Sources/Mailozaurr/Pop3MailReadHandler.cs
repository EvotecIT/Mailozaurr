using MailKit.Net.Pop3;
using MimeKit;
using System.Security.Cryptography;

namespace Mailozaurr;

/// <summary>
/// Normalized POP3 read handler backed by Mailozaurr POP3 helpers.
/// </summary>
/// <remarks>
/// POP3 exposes a single inbox and does not provide server-side folders or persistent read/move state.
/// </remarks>
public sealed class Pop3MailReadHandler : IMailReadHandler {
    private const string Inbox = "INBOX";
    private readonly IPop3SessionFactory _sessionFactory;
    private readonly Func<Pop3Client, MailProfile, MailFolderQuery, CancellationToken, Task<IReadOnlyList<FolderRef>>> _getFoldersAsync;
    private readonly Func<Pop3Client, MailProfile, MailSearchRequest, CancellationToken, Task<IReadOnlyList<MessageSummary>>> _searchAsync;
    private readonly Func<Pop3Client, MailProfile, GetMessageRequest, CancellationToken, Task<MessageDetail?>> _getMessageAsync;
    private readonly Func<Pop3Client, MailProfile, SaveAttachmentRequest, CancellationToken, Task<OperationResult>> _saveAttachmentAsync;

    /// <summary>Creates a new POP3 read handler.</summary>
    public Pop3MailReadHandler(
        IPop3SessionFactory sessionFactory,
        Func<Pop3Client, MailProfile, MailFolderQuery, CancellationToken, Task<IReadOnlyList<FolderRef>>>? getFoldersAsync = null,
        Func<Pop3Client, MailProfile, MailSearchRequest, CancellationToken, Task<IReadOnlyList<MessageSummary>>>? searchAsync = null,
        Func<Pop3Client, MailProfile, GetMessageRequest, CancellationToken, Task<MessageDetail?>>? getMessageAsync = null,
        Func<Pop3Client, MailProfile, SaveAttachmentRequest, CancellationToken, Task<OperationResult>>? saveAttachmentAsync = null) {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _getFoldersAsync = getFoldersAsync ?? DefaultGetFoldersAsync;
        _searchAsync = searchAsync ?? DefaultSearchAsync;
        _getMessageAsync = getMessageAsync ?? DefaultGetMessageAsync;
        _saveAttachmentAsync = saveAttachmentAsync ?? DefaultSaveAttachmentAsync;
    }

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.Pop3;

    /// <inheritdoc />
    public async Task<IReadOnlyList<FolderRef>> GetFoldersAsync(
        MailProfile profile,
        MailFolderQuery query,
        CancellationToken cancellationToken = default) {
        using var client = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _getFoldersAsync(client, profile, query, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MessageSummary>> SearchAsync(
        MailProfile profile,
        MailSearchRequest request,
        CancellationToken cancellationToken = default) {
        using var client = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _searchAsync(client, profile, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MessageDetail?> GetMessageAsync(
        MailProfile profile,
        GetMessageRequest request,
        CancellationToken cancellationToken = default) {
        using var client = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _getMessageAsync(client, profile, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> SaveAttachmentAsync(
        MailProfile profile,
        SaveAttachmentRequest request,
        CancellationToken cancellationToken = default) {
        using var client = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _saveAttachmentAsync(client, profile, request, cancellationToken).ConfigureAwait(false);
    }

    private static Task<IReadOnlyList<FolderRef>> DefaultGetFoldersAsync(
        Pop3Client client,
        MailProfile profile,
        MailFolderQuery query,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<FolderRef> folders = string.IsNullOrWhiteSpace(query.ParentFolderId)
            ? new[] {
                new FolderRef {
                    ProfileId = profile.Id,
                    Id = Inbox,
                    DisplayName = "Inbox",
                    Path = Inbox,
                    SpecialUse = "Inbox",
                    MessageCount = client.Count
                }
            }
            : Array.Empty<FolderRef>();
        return Task.FromResult(folders);
    }

    private static async Task<IReadOnlyList<MessageSummary>> DefaultSearchAsync(
        Pop3Client client,
        MailProfile profile,
        MailSearchRequest request,
        CancellationToken cancellationToken) {
        _ = NormalizeFolderId(request.FolderId);
        if (request.IsRead.HasValue) {
            throw new InvalidOperationException("POP3 does not expose persistent read state.");
        }

        var messages = await MailboxSearcher.SearchPop3Async(
            client,
            subject: request.SubjectContains,
            fromContains: request.FromContains,
            toContains: request.ToContains,
            since: request.Since?.UtcDateTime,
            before: request.Before?.UtcDateTime,
            hasAttachment: request.HasAttachments,
            maxResults: request.Limit ?? 0,
            cancellationToken: cancellationToken,
            queryString: request.QueryText).ConfigureAwait(false);

        var results = new List<MessageSummary>(messages.Count);
        var fingerprintOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var message in messages) {
            var fingerprint = ComputeMessageFingerprint(message.Message);
            _ = fingerprintOccurrences.TryGetValue(fingerprint, out var occurrence);
            fingerprintOccurrences[fingerprint] = occurrence + 1;
            var uid = await TryGetUidAsync(client, message.Index, cancellationToken).ConfigureAwait(false);
            results.Add(MapSummary(profile.Id, uid, message.Message, occurrence));
        }
        return results;
    }

    private static async Task<MessageDetail?> DefaultGetMessageAsync(
        Pop3Client client,
        MailProfile profile,
        GetMessageRequest request,
        CancellationToken cancellationToken) {
        _ = NormalizeFolderId(request.FolderId);
        var identifier = ParseMessageId(request.MessageId);
        var resolved = await ResolveMessageAsync(client, identifier, cancellationToken).ConfigureAwait(false);
        if (resolved.Status == Pop3MailboxBrowser.Pop3MessageResolveStatus.NotFound) {
            return null;
        }
        if (resolved.Status != Pop3MailboxBrowser.Pop3MessageResolveStatus.Success || resolved.Snapshot == null) {
            throw new InvalidOperationException($"POP3 message '{request.MessageId}' could not be resolved ({resolved.Status}).");
        }

        return MapDetail(profile.Id, resolved.Snapshot, request.IncludeRawContent, identifier);
    }

    private static async Task<OperationResult> DefaultSaveAttachmentAsync(
        Pop3Client client,
        MailProfile profile,
        SaveAttachmentRequest request,
        CancellationToken cancellationToken) {
        var folderId = NormalizeFolderId(request.FolderId);
        var identifier = ParseMessageId(request.MessageId);
        var resolved = await ResolveMessageAsync(client, identifier, cancellationToken).ConfigureAwait(false);
        if (resolved.Status != Pop3MailboxBrowser.Pop3MessageResolveStatus.Success || resolved.Snapshot == null) {
            return OperationResult.Failure("message_not_found", $"POP3 message '{request.MessageId}' was not found.");
        }

        var attachments = resolved.Snapshot.Message.Attachments.ToList();
        var attachmentIndex = MimeAttachmentStorage.ResolveAttachmentIndex(attachments, request.AttachmentId);
        if (attachmentIndex < 0) {
            return OperationResult.Failure("attachment_not_found", $"Attachment '{request.AttachmentId}' was not found.");
        }
        var attachment = attachments[attachmentIndex];
        var canonicalMessageId = CanonicalizeMessageIdForStorage(identifier, resolved.Snapshot);

        var destinationPath = MimeAttachmentStorage.ResolveDestinationPath(
            request.DestinationPath,
            attachment,
            MimeAttachmentStorage.CreateStorageIdentity(
                profile.Id,
                profile.Kind.ToString(),
                folderId,
                canonicalMessageId,
                attachmentIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (File.Exists(destinationPath) && !request.Overwrite) {
            return OperationResult.Failure("destination_exists", $"Destination '{destinationPath}' already exists.");
        }

        MimeAttachmentStorage.SaveAttachment(attachment, destinationPath);
        return OperationResult.Success($"Attachment saved to '{destinationPath}'.");
    }

    internal static string FormatMessageId(string? uid, MimeMessage message, int occurrence = 0) {
        if (occurrence < 0) {
            throw new ArgumentOutOfRangeException(nameof(occurrence));
        }
        return string.IsNullOrWhiteSpace(uid)
            ? $"hash:{ComputeMessageFingerprint(message)}:{occurrence.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : $"uid:{uid}";
    }

    internal static string CanonicalizeMessageIdForStorage(
        (string? Uid, string? Fingerprint, int Occurrence) identifier,
        Pop3MailboxBrowser.Pop3ResolvedMessageSnapshot snapshot) =>
        string.IsNullOrWhiteSpace(identifier.Fingerprint)
            ? FormatMessageId(snapshot.Uid, snapshot.Message)
            : FormatMessageId(null, snapshot.Message, identifier.Occurrence);

    internal static (string? Uid, string? Fingerprint, int Occurrence) ParseMessageId(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new InvalidOperationException("A POP3 message id is required.");
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("uid:", StringComparison.OrdinalIgnoreCase)) {
            var uid = normalized.Substring(4);
            if (string.IsNullOrWhiteSpace(uid)) {
                throw new InvalidOperationException("A POP3 UID value is required after 'uid:'.");
            }
            return (uid, null, 0);
        }
        if (normalized.StartsWith("hash:", StringComparison.OrdinalIgnoreCase)) {
            var fingerprintWithOccurrence = normalized.Substring(5);
            var separatorIndex = fingerprintWithOccurrence.LastIndexOf(':');
            var fingerprint = separatorIndex < 0
                ? fingerprintWithOccurrence
                : fingerprintWithOccurrence.Substring(0, separatorIndex);
            if (string.IsNullOrWhiteSpace(fingerprint)) {
                throw new InvalidOperationException("A POP3 content fingerprint is required after 'hash:'.");
            }
            var occurrence = 0;
            if (separatorIndex >= 0 &&
                (!int.TryParse(
                    fingerprintWithOccurrence.Substring(separatorIndex + 1),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out occurrence) || occurrence < 0)) {
                throw new InvalidOperationException("A POP3 hash occurrence must be a non-negative integer.");
            }
            return (null, fingerprint, occurrence);
        }
        if (normalized.StartsWith("index:", StringComparison.OrdinalIgnoreCase) ||
            int.TryParse(normalized, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _)) {
            throw new InvalidOperationException(
                "Session-local POP3 index ids are not reusable. Use the uid: or hash: id returned by search.");
        }

        return (normalized, null, 0);
    }

    internal static MessageSummary MapSummary(
        string profileId,
        string? uid,
        MimeMessage message,
        int occurrence = 0) => new() {
        ProfileId = profileId,
        Id = FormatMessageId(uid, message, occurrence),
        FolderId = Inbox,
        Subject = message.Subject,
        Preview = CreatePreview(message.TextBody),
        From = MapRecipients(message.From),
        To = MapRecipients(message.To),
        Cc = MapRecipients(message.Cc),
        SentAt = message.Date,
        ReceivedAt = message.Date,
        HasAttachments = message.Attachments.Any(),
        Priority = message.Priority switch {
            MimeKit.MessagePriority.Urgent => MessagePriority.High,
            MimeKit.MessagePriority.NonUrgent => MessagePriority.Low,
            _ => MessagePriority.Normal
        }
    };

    private static MessageDetail MapDetail(
        string profileId,
        Pop3MailboxBrowser.Pop3ResolvedMessageSnapshot snapshot,
        bool includeRawContent,
        (string? Uid, string? Fingerprint, int Occurrence) identifier) {
        var id = string.IsNullOrWhiteSpace(identifier.Fingerprint)
            ? FormatMessageId(snapshot.Uid, snapshot.Message)
            : FormatMessageId(null, snapshot.Message, identifier.Occurrence);
        var detail = new MessageDetail {
            ProfileId = profileId,
            Id = id,
            Summary = MapSummary(
                profileId,
                string.IsNullOrWhiteSpace(identifier.Fingerprint) ? snapshot.Uid : null,
                snapshot.Message,
                identifier.Occurrence),
            TextBody = snapshot.Message.TextBody,
            HtmlBody = snapshot.Message.HtmlBody,
            Attachments = snapshot.Message.Attachments.Select((attachment, index) => new AttachmentSummary {
                MessageId = id,
                Id = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                FileName = MimeAttachmentStorage.GetAttachmentFileName(
                    attachment,
                    MimeAttachmentStorage.CreateStorageIdentity(profileId, id, index.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                ContentType = attachment.ContentType.MimeType
            }).ToList()
        };

        if (includeRawContent) {
            using var stream = new MemoryStream();
            snapshot.Message.WriteTo(stream);
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            detail.RawContent = reader.ReadToEnd();
        }
        return detail;
    }

    private static async Task<string?> TryGetUidAsync(
        Pop3Client client,
        int index,
        CancellationToken cancellationToken) {
        try {
            return await client.GetMessageUidAsync(index, cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) {
            throw;
        } catch (NotSupportedException) {
            return null;
        } catch (Pop3CommandException) {
            return null;
        }
    }

    private static async Task<Pop3MailboxBrowser.Pop3MessageResolveResult> ResolveMessageAsync(
        Pop3Client client,
        (string? Uid, string? Fingerprint, int Occurrence) identifier,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(identifier.Fingerprint)) {
            return await Pop3MailboxBrowser.ResolveMessageAsync(
                client,
                requestedIndex: null,
                requestedUid: identifier.Uid,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var occurrence = 0;
        for (var index = client.Count - 1; index >= 0; index--) {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = await Pop3MailboxBrowser.ResolveMessageAsync(
                client,
                index,
                requestedUid: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (candidate.Status == Pop3MailboxBrowser.Pop3MessageResolveStatus.Success &&
                candidate.Snapshot != null &&
                string.Equals(
                    ComputeMessageFingerprint(candidate.Snapshot.Message),
                    identifier.Fingerprint,
                    StringComparison.Ordinal)) {
                if (occurrence == identifier.Occurrence) {
                    return candidate;
                }
                occurrence++;
            }
        }

        return new Pop3MailboxBrowser.Pop3MessageResolveResult(
            Pop3MailboxBrowser.Pop3MessageResolveStatus.NotFound,
            null);
    }

    internal static string ComputeMessageFingerprint(MimeMessage message) {
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }

        using var stream = new MemoryStream();
        message.WriteTo(stream);
        stream.Position = 0;
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(stream);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    internal static string NormalizeFolderId(string? folderId) {
        if (!string.IsNullOrWhiteSpace(folderId) &&
            !string.Equals(folderId!.Trim(), Inbox, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException($"POP3 exposes only the '{Inbox}' folder.");
        }
        return Inbox;
    }

    private static List<MessageRecipient> MapRecipients(InternetAddressList addresses) =>
        addresses.Mailboxes.Select(mailbox => new MessageRecipient {
            Name = mailbox.Name,
            Address = mailbox.Address ?? string.Empty
        }).ToList();

    private static string? CreatePreview(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return value;
        }
        const int maxLength = 512;
        return value!.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}
