using MailKit;
using MailKit.Net.Imap;

namespace Mailozaurr.Application;

/// <summary>
/// Normalized IMAP mailbox action handler backed by Mailozaurr IMAP helpers.
/// </summary>
public sealed class ImapMailMessageActionHandler : IMailMessageActionHandler {
    private readonly IImapSessionFactory _sessionFactory;
    private readonly Func<ImapClient, MailProfile, SetReadStateRequest, CancellationToken, Task<MessageActionResult>> _setReadStateAsync;
    private readonly Func<ImapClient, MailProfile, MoveMessagesRequest, CancellationToken, Task<MessageActionResult>> _moveAsync;
    private readonly Func<ImapClient, MailProfile, DeleteMessagesRequest, CancellationToken, Task<MessageActionResult>> _deleteAsync;

    /// <summary>
    /// Creates a new IMAP message-action handler.
    /// </summary>
    public ImapMailMessageActionHandler(
        IImapSessionFactory sessionFactory,
        Func<ImapClient, MailProfile, SetReadStateRequest, CancellationToken, Task<MessageActionResult>>? setReadStateAsync = null,
        Func<ImapClient, MailProfile, MoveMessagesRequest, CancellationToken, Task<MessageActionResult>>? moveAsync = null,
        Func<ImapClient, MailProfile, DeleteMessagesRequest, CancellationToken, Task<MessageActionResult>>? deleteAsync = null) {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _setReadStateAsync = setReadStateAsync ?? DefaultSetReadStateAsync;
        _moveAsync = moveAsync ?? DefaultMoveAsync;
        _deleteAsync = deleteAsync ?? DefaultDeleteAsync;
    }

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.Imap;

    /// <inheritdoc />
    public async Task<MessageActionResult> SetReadStateAsync(MailProfile profile, SetReadStateRequest request, CancellationToken cancellationToken = default) {
        using var client = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _setReadStateAsync(client, profile, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MessageActionResult> SetFlaggedStateAsync(MailProfile profile, SetFlaggedStateRequest request, CancellationToken cancellationToken = default) {
        using var client = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        var folder = client.GetCachedFolder(ResolveFolder(request.FolderId, profile), FolderAccess.ReadWrite);
        var operation = await ImapBulkFlagOperations.SetFlagsAsync(
            folder,
            ParseUids(request.MessageIds),
            MessageFlags.Flagged,
            add: request.IsFlagged,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new MessageActionResult {
            Succeeded = operation.Results.All(item => item.Ok) && operation.Updated > 0,
            Code = operation.Results.All(item => item.Ok) && operation.Updated > 0 ? null : "message_action_failed",
            Message = operation.Results.All(item => item.Ok) && operation.Updated > 0
                ? (request.IsFlagged ? "Flagged messages." : "Unflagged messages.")
                : $"{operation.Updated} message action(s) succeeded; {operation.Results.Count(item => !item.Ok)} failed.",
            ProfileId = profile.Id,
            RequestedCount = operation.Requested,
            SucceededCount = operation.Updated,
            FailedCount = operation.Results.Count(item => !item.Ok),
            Results = operation.Results.Select(item => new MessageActionItemResult {
                MessageId = item.Uid.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Succeeded = item.Ok,
                Code = item.Ok ? null : "message_action_failed",
                Message = item.Error
            }).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<MessageActionResult> MoveAsync(MailProfile profile, MoveMessagesRequest request, CancellationToken cancellationToken = default) {
        using var client = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _moveAsync(client, profile, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MessageActionResult> DeleteAsync(MailProfile profile, DeleteMessagesRequest request, CancellationToken cancellationToken = default) {
        using var client = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _deleteAsync(client, profile, request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<MessageActionResult> DefaultSetReadStateAsync(ImapClient client, MailProfile profile, SetReadStateRequest request, CancellationToken cancellationToken) {
        var folder = client.GetCachedFolder(ResolveFolder(request.FolderId, profile), FolderAccess.ReadWrite);
        var operation = await ImapBulkFlagOperations.SetFlagsAsync(
            folder,
            ParseUids(request.MessageIds),
            MessageFlags.Seen,
            add: request.IsRead,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new MessageActionResult {
            Succeeded = operation.Results.All(item => item.Ok) && operation.Updated > 0,
            Code = operation.Results.All(item => item.Ok) && operation.Updated > 0 ? null : "message_action_failed",
            Message = operation.Results.All(item => item.Ok) && operation.Updated > 0
                ? (request.IsRead ? "Marked messages as read." : "Marked messages as unread.")
                : $"{operation.Updated} message action(s) succeeded; {operation.Results.Count(item => !item.Ok)} failed.",
            ProfileId = profile.Id,
            RequestedCount = operation.Requested,
            SucceededCount = operation.Updated,
            FailedCount = operation.Results.Count(item => !item.Ok),
            Results = operation.Results.Select(item => new MessageActionItemResult {
                MessageId = item.Uid.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Succeeded = item.Ok,
                Code = item.Ok ? null : "message_action_failed",
                Message = item.Error
            }).ToList()
        };
    }

    private static async Task<MessageActionResult> DefaultMoveAsync(ImapClient client, MailProfile profile, MoveMessagesRequest request, CancellationToken cancellationToken) {
        var operation = await ImapMoveOperations.MoveAsync(
            client,
            ResolveFolder(request.FolderId, profile),
            request.DestinationFolderId,
            ParseUids(request.MessageIds),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new MessageActionResult {
            Succeeded = operation.Results.All(item => item.Ok) && operation.Moved > 0,
            Code = operation.Results.All(item => item.Ok) && operation.Moved > 0 ? null : "message_action_failed",
            Message = operation.Results.All(item => item.Ok) && operation.Moved > 0
                ? $"Moved messages to '{request.DestinationFolderId}'."
                : $"{operation.Moved} message action(s) succeeded; {operation.Results.Count(item => !item.Ok)} failed.",
            ProfileId = profile.Id,
            RequestedCount = operation.Requested,
            SucceededCount = operation.Moved,
            FailedCount = operation.Results.Count(item => !item.Ok),
            Results = operation.Results.Select(item => new MessageActionItemResult {
                MessageId = item.Uid.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Succeeded = item.Ok,
                Code = item.Ok ? null : "message_action_failed",
                Message = item.Error
            }).ToList()
        };
    }

    private static async Task<MessageActionResult> DefaultDeleteAsync(ImapClient client, MailProfile profile, DeleteMessagesRequest request, CancellationToken cancellationToken) {
        var folder = client.GetCachedFolder(ResolveFolder(request.FolderId, profile), FolderAccess.ReadWrite);
        var operation = await ImapDeleteOperations.DeleteAsync(
            folder,
            ParseUids(request.MessageIds),
            expunge: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new MessageActionResult {
            Succeeded = operation.Results.All(item => item.Ok) && operation.Deleted > 0,
            Code = operation.Results.All(item => item.Ok) && operation.Deleted > 0 ? null : "message_action_failed",
            Message = operation.Results.All(item => item.Ok) && operation.Deleted > 0
                ? "Deleted messages."
                : $"{operation.Deleted} message action(s) succeeded; {operation.Results.Count(item => !item.Ok)} failed.",
            ProfileId = profile.Id,
            RequestedCount = operation.Requested,
            SucceededCount = operation.Deleted,
            FailedCount = operation.Results.Count(item => !item.Ok),
            Results = operation.Results.Select(item => new MessageActionItemResult {
                MessageId = item.Uid.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Succeeded = item.Ok,
                Code = item.Ok ? null : "message_action_failed",
                Message = item.Error
            }).ToList()
        };
    }

    private static IReadOnlyList<UniqueId> ParseUids(IEnumerable<string> messageIds) =>
        messageIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(ParseUid).ToArray();

    private static UniqueId ParseUid(string value) {
        if (uint.TryParse(value.Trim(), out var uid)) {
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
}