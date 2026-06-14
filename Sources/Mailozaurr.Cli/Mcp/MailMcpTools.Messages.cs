using Mailozaurr.Application;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Mailozaurr.Cli.Mcp;

public sealed partial class MailMcpTools {
    [McpServerTool]
    [Description("Searches messages in a mailbox using normalized Mailozaurr filters.")]
    public Task<IReadOnlyList<MessageSummary>> mail_search(
        [Description("The profile identifier to query.")] string profileId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier to scope the search.")] string? folderId = null,
        [Description("Optional free-text query to match across provider-specific searchable content.")] string? queryText = null,
        [Description("Optional subject text filter.")] string? subjectContains = null,
        [Description("Optional sender text filter.")] string? fromContains = null,
        [Description("Optional recipient text filter.")] string? toContains = null,
        [Description("When true, only returns messages with attachments.")] bool hasAttachments = false,
        [Description("Optional maximum number of messages to return.")] int? limit = null,
        CancellationToken cancellationToken = default) =>
        _application.Read.SearchAsync(new MailSearchRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            QueryText = queryText,
            SubjectContains = subjectContains,
            FromContains = fromContains,
            ToContains = toContains,
            HasAttachments = hasAttachments,
            Limit = limit
        }, cancellationToken);

    [McpServerTool]
    [Description("Searches messages in a mailbox using a lightweight Mailozaurr message projection.")]
    public Task<IReadOnlyList<MessageSummaryCompact>> mail_search_compact(
        [Description("The profile identifier to query.")] string profileId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier to scope the search.")] string? folderId = null,
        [Description("Optional free-text query to match across provider-specific searchable content.")] string? queryText = null,
        [Description("Optional subject text filter.")] string? subjectContains = null,
        [Description("Optional sender text filter.")] string? fromContains = null,
        [Description("Optional recipient text filter.")] string? toContains = null,
        [Description("When true, only returns messages with attachments.")] bool hasAttachments = false,
        [Description("Optional maximum number of messages to return.")] int? limit = null,
        CancellationToken cancellationToken = default) =>
        _application.Read.SearchCompactAsync(new MailSearchRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            QueryText = queryText,
            SubjectContains = subjectContains,
            FromContains = fromContains,
            ToContains = toContains,
            HasAttachments = hasAttachments,
            Limit = limit
        }, cancellationToken);

    [McpServerTool]
    [Description("Gets a detailed message view for a specific message identifier.")]
    public async Task<MessageDetail> mail_get(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifier to retrieve.")] string messageId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("When true, includes raw provider content where supported.")] bool includeRawContent = false,
        CancellationToken cancellationToken = default) {
        var message = await _application.Read.GetMessageAsync(new GetMessageRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageId = messageId,
            IncludeRawContent = includeRawContent
        }, cancellationToken).ConfigureAwait(false);

        return message ?? throw new InvalidOperationException($"Message '{messageId}' was not found.");
    }

    [McpServerTool]
    [Description("Gets a lightweight detailed message view for a specific message identifier.")]
    public async Task<MessageDetailCompact> mail_get_compact(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifier to retrieve.")] string messageId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("When true, allows the provider to include raw-content presence in the projection.")] bool includeRawContent = false,
        CancellationToken cancellationToken = default) {
        var message = await _application.Read.GetMessageCompactAsync(new GetMessageRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageId = messageId,
            IncludeRawContent = includeRawContent
        }, cancellationToken).ConfigureAwait(false);

        return message ?? throw new InvalidOperationException($"Message '{messageId}' was not found.");
    }

    [McpServerTool]
    [Description("Gets detailed message views for multiple specific message identifiers.")]
    public Task<IReadOnlyList<MessageDetail>> mail_get_many(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to retrieve.")] string[] messageIds,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("When true, includes raw provider content where supported.")] bool includeRawContent = false,
        CancellationToken cancellationToken = default) =>
        _application.Read.GetMessagesAsync(new GetMessagesRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            IncludeRawContent = includeRawContent
        }, cancellationToken);

    [McpServerTool]
    [Description("Gets lightweight detailed message views for multiple specific message identifiers.")]
    public Task<IReadOnlyList<MessageDetailCompact>> mail_get_many_compact(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to retrieve.")] string[] messageIds,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("When true, includes raw-content presence where supported.")] bool includeRawContent = false,
        CancellationToken cancellationToken = default) =>
        _application.Read.GetMessagesCompactAsync(new GetMessagesRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            IncludeRawContent = includeRawContent
        }, cancellationToken);

    [McpServerTool]
    [Description("Builds a dry-run preview for changing messages to read or unread, including normalized message ids and a reusable confirmation token.")]
    public Task<MessageStateChangePreview> mail_mark_read_preview(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to preview.")] string[] messageIds,
        [Description("Desired read state. True marks as read; false marks as unread.")] bool isRead = true,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPreview.PreviewReadStateAsync(new SetReadStateRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            IsRead = isRead
        }, cancellationToken);

    [McpServerTool]
    [Description("Marks one or more messages as read or unread using the shared Mailozaurr message-action service.")]
    public Task<MessageActionResult> mail_mark_read(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to update.")] string[] messageIds,
        [Description("Desired read state. True marks as read; false marks as unread.")] bool isRead = true,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActions.SetReadStateAsync(new SetReadStateRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            IsRead = isRead,
            ConfirmationToken = confirmationToken
        }, cancellationToken);

    [McpServerTool]
    [Description("Builds a dry-run preview for flagging or unflagging messages, including normalized message ids and a reusable confirmation token.")]
    public Task<MessageStateChangePreview> mail_flag_preview(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to preview.")] string[] messageIds,
        [Description("Desired flagged state. True flags/star-marks; false unflags.")] bool isFlagged = true,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActionPreview.PreviewFlaggedStateAsync(new SetFlaggedStateRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            IsFlagged = isFlagged
        }, cancellationToken);

    [McpServerTool]
    [Description("Flags or unflags one or more messages using the shared Mailozaurr message-action service.")]
    public Task<MessageActionResult> mail_flag(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to update.")] string[] messageIds,
        [Description("Desired flagged state. True flags/star-marks; false unflags.")] bool isFlagged = true,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActions.SetFlaggedStateAsync(new SetFlaggedStateRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            IsFlagged = isFlagged,
            ConfirmationToken = confirmationToken
        }, cancellationToken);

    [McpServerTool]
    [Description("Archives one or more messages using the shared Mailozaurr message-action service and a provider-neutral Archive alias.")]
    public Task<MessageActionResult> mail_archive(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to archive.")] string[] messageIds,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActions.MoveAsync(new MoveMessagesRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            DestinationFolderId = MailFolderAliases.Archive,
            ConfirmationToken = confirmationToken
        }, cancellationToken);

    [McpServerTool]
    [Description("Moves one or more messages to trash using the shared Mailozaurr message-action service and a provider-neutral Trash alias.")]
    public Task<MessageActionResult> mail_trash(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to trash.")] string[] messageIds,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActions.MoveAsync(new MoveMessagesRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            DestinationFolderId = MailFolderAliases.Trash,
            ConfirmationToken = confirmationToken
        }, cancellationToken);

    [McpServerTool]
    [Description("Moves one or more messages to a destination folder using the shared Mailozaurr message-action service.")]
    public Task<MessageActionResult> mail_move(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to move.")] string[] messageIds,
        [Description("Destination folder identifier or provider alias.")] string destinationFolderId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional source folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActions.MoveAsync(new MoveMessagesRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            DestinationFolderId = destinationFolderId,
            ConfirmationToken = confirmationToken
        }, cancellationToken);

    [McpServerTool]
    [Description("Deletes one or more messages using the shared Mailozaurr message-action service.")]
    public Task<MessageActionResult> mail_delete(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers to delete.")] string[] messageIds,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional confirmation token returned by a matching preview call.")] string? confirmationToken = null,
        CancellationToken cancellationToken = default) =>
        _application.MessageActions.DeleteAsync(new DeleteMessagesRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            ConfirmationToken = confirmationToken
        }, cancellationToken);

    [McpServerTool]
    [Description("Lists attachment metadata for a specific message without returning the full message body.")]
    public Task<IReadOnlyList<AttachmentSummary>> mail_attachments_list(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifier that owns the attachments.")] string messageId,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        CancellationToken cancellationToken = default) =>
        _application.Read.GetAttachmentsAsync(new ListAttachmentsRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageId = messageId
        }, cancellationToken);

    [McpServerTool]
    [Description("Saves a message attachment to a local path that the Mailozaurr server can access.")]
    public Task<OperationResult> mail_attachment_save(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifier that owns the attachment.")] string messageId,
        [Description("The provider-specific attachment identifier to save.")] string attachmentId,
        [Description("The destination file path on the server filesystem.")] string destinationPath,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("When true, allows an existing destination file to be overwritten.")] bool overwrite = false,
        CancellationToken cancellationToken = default) =>
        _application.Read.SaveAttachmentAsync(new SaveAttachmentRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageId = messageId,
            AttachmentId = attachmentId,
            DestinationPath = destinationPath,
            Overwrite = overwrite
        }, cancellationToken);

    [McpServerTool]
    [Description("Saves one or more attachments from a message using shared Mailozaurr filtering and batching logic.")]
    public Task<SaveAttachmentsResult> mail_attachments_save(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifier that owns the attachments.")] string messageId,
        [Description("The destination path or directory on the server filesystem.")] string destinationPath,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional explicit attachment identifiers to save.")] string[]? attachmentIds = null,
        [Description("Optional case-insensitive file-name filter.")] string? fileNameContains = null,
        [Description("Optional case-insensitive content-type filter.")] string? contentTypeContains = null,
        [Description("When true, allows existing destination files to be overwritten.")] bool overwrite = false,
        CancellationToken cancellationToken = default) =>
        _application.Read.SaveAttachmentsAsync(new SaveAttachmentsRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageId = messageId,
            DestinationPath = destinationPath,
            AttachmentIds = attachmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            FileNameContains = fileNameContains,
            ContentTypeContains = contentTypeContains,
            Overwrite = overwrite
        }, cancellationToken);

    [McpServerTool]
    [Description("Saves attachments from multiple messages using shared Mailozaurr filtering and batching logic.")]
    public Task<SaveAttachmentsManyResult> mail_attachments_save_many(
        [Description("The profile identifier to query.")] string profileId,
        [Description("The provider-specific message identifiers that own the attachments.")] string[] messageIds,
        [Description("The destination path or directory on the server filesystem.")] string destinationPath,
        [Description("Optional mailbox identifier for providers that support multiple mailboxes.")] string? mailboxId = null,
        [Description("Optional folder identifier when the provider requires folder scoping.")] string? folderId = null,
        [Description("Optional explicit attachment identifiers to save.")] string[]? attachmentIds = null,
        [Description("Optional case-insensitive file-name filter.")] string? fileNameContains = null,
        [Description("Optional case-insensitive content-type filter.")] string? contentTypeContains = null,
        [Description("When true, allows existing destination files to be overwritten.")] bool overwrite = false,
        CancellationToken cancellationToken = default) =>
        _application.Read.SaveAttachmentsManyAsync(new SaveAttachmentsManyRequest {
            ProfileId = profileId,
            MailboxId = mailboxId,
            FolderId = folderId,
            MessageIds = messageIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            DestinationPath = destinationPath,
            AttachmentIds = attachmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList() ?? new List<string>(),
            FileNameContains = fileNameContains,
            ContentTypeContains = contentTypeContains,
            Overwrite = overwrite
        }, cancellationToken);

    [McpServerTool]
    [Description("Sends or queues a message using a configured Mailozaurr profile. Queueing is the default unless sendNow is true.")]
    public Task<SendResult> mail_send(
        [Description("The profile identifier to use for sending.")] string profileId,
        [Description("Primary recipient email addresses.")] string[] to,
        [Description("Optional subject line.")] string? subject = null,
        [Description("Optional plain text body.")] string? textBody = null,
        [Description("Optional HTML body.")] string? htmlBody = null,
        [Description("Optional CC recipient email addresses.")] string[]? cc = null,
        [Description("Optional BCC recipient email addresses.")] string[]? bcc = null,
        [Description("Optional Reply-To recipient email addresses.")] string[]? replyTo = null,
        [Description("Optional From email address override.")] string? from = null,
        [Description("Optional attachment file paths on the server filesystem.")] string[]? attachmentPaths = null,
        [Description("When true, sends immediately instead of preferring the queue.")] bool sendNow = false,
        CancellationToken cancellationToken = default) =>
        _application.Send.SendAsync(new SendMessageRequest {
            ProfileId = profileId,
            PreferQueue = !sendNow,
            RequireImmediateSend = sendNow,
            Message = BuildDraftMessage(profileId, to, subject, textBody, htmlBody, cc, bcc, replyTo, from, attachmentPaths)
        }, cancellationToken);
}