namespace Mailozaurr.Hosting;

/// <summary>
/// Normalized Graph mailbox action handler backed by Mailozaurr Graph helpers.
/// </summary>
public sealed class GraphMailMessageActionHandler : IMailMessageActionHandler {
    private readonly IGraphSessionFactory _sessionFactory;
    private readonly Func<GraphSession, MailProfile, SetReadStateRequest, CancellationToken, Task<MessageActionResult>> _setReadStateAsync;
    private readonly Func<GraphSession, MailProfile, MoveMessagesRequest, CancellationToken, Task<MessageActionResult>> _moveAsync;
    private readonly Func<GraphSession, MailProfile, DeleteMessagesRequest, CancellationToken, Task<MessageActionResult>> _deleteAsync;

    /// <summary>
    /// Creates a new Graph message-action handler.
    /// </summary>
    public GraphMailMessageActionHandler(
        IGraphSessionFactory sessionFactory,
        Func<GraphSession, MailProfile, SetReadStateRequest, CancellationToken, Task<MessageActionResult>>? setReadStateAsync = null,
        Func<GraphSession, MailProfile, MoveMessagesRequest, CancellationToken, Task<MessageActionResult>>? moveAsync = null,
        Func<GraphSession, MailProfile, DeleteMessagesRequest, CancellationToken, Task<MessageActionResult>>? deleteAsync = null) {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _setReadStateAsync = setReadStateAsync ?? DefaultSetReadStateAsync;
        _moveAsync = moveAsync ?? DefaultMoveAsync;
        _deleteAsync = deleteAsync ?? DefaultDeleteAsync;
    }

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.Graph;

    /// <inheritdoc />
    public async Task<MessageActionResult> SetReadStateAsync(MailProfile profile, SetReadStateRequest request, CancellationToken cancellationToken = default) {
        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _setReadStateAsync(session, profile, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MessageActionResult> SetFlaggedStateAsync(MailProfile profile, SetFlaggedStateRequest request, CancellationToken cancellationToken = default) {
        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        var browser = new GraphMailboxBrowser(session.Client);
        var results = await browser.SetMessagesFlaggedAsync(
            request.MessageIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToArray(),
            request.IsFlagged,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return MapResult(profile.Id, results, request.IsFlagged ? "Flagged messages." : "Unflagged messages.");
    }

    /// <inheritdoc />
    public async Task<MessageActionResult> MoveAsync(MailProfile profile, MoveMessagesRequest request, CancellationToken cancellationToken = default) {
        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _moveAsync(session, profile, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MessageActionResult> DeleteAsync(MailProfile profile, DeleteMessagesRequest request, CancellationToken cancellationToken = default) {
        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _deleteAsync(session, profile, request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<MessageActionResult> DefaultSetReadStateAsync(GraphSession session, MailProfile profile, SetReadStateRequest request, CancellationToken cancellationToken) {
        var browser = new GraphMailboxBrowser(session.Client);
        var results = await browser.SetMessagesSeenAsync(NormalizeIds(request.MessageIds), request.IsRead, cancellationToken: cancellationToken).ConfigureAwait(false);
        return MapResult(profile.Id, results, request.IsRead ? "Marked messages as read." : "Marked messages as unread.");
    }

    private static async Task<MessageActionResult> DefaultMoveAsync(GraphSession session, MailProfile profile, MoveMessagesRequest request, CancellationToken cancellationToken) {
        var browser = new GraphMailboxBrowser(session.Client);
        var results = await browser.MoveMessagesAsync(NormalizeIds(request.MessageIds), request.DestinationFolderId, cancellationToken: cancellationToken).ConfigureAwait(false);
        return MapResult(profile.Id, results, $"Moved messages to '{request.DestinationFolderId}'.");
    }

    private static async Task<MessageActionResult> DefaultDeleteAsync(GraphSession session, MailProfile profile, DeleteMessagesRequest request, CancellationToken cancellationToken) {
        var browser = new GraphMailboxBrowser(session.Client);
        var results = await browser.DeleteMessagesAsync(NormalizeIds(request.MessageIds), cancellationToken: cancellationToken).ConfigureAwait(false);
        return MapResult(profile.Id, results, "Deleted messages.");
    }

    private static IReadOnlyList<string> NormalizeIds(IEnumerable<string> messageIds) =>
        messageIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToArray();

    private static MessageActionResult MapResult(string profileId, IReadOnlyList<MailboxBulkOperationResult> items, string successMessage) {
        var result = new MessageActionResult {
            ProfileId = profileId,
            RequestedCount = items.Count
        };

        foreach (var item in items) {
            result.Results.Add(new MessageActionItemResult {
                MessageId = item.Id,
                Succeeded = item.Ok,
                Code = item.Ok ? null : "message_action_failed",
                Message = item.Ok ? null : item.Error
            });
            if (item.Ok) {
                result.SucceededCount++;
            } else {
                result.FailedCount++;
            }
        }

        result.Succeeded = result.FailedCount == 0 && result.SucceededCount > 0;
        result.Code = result.Succeeded ? null : "message_action_failed";
        result.Message = result.Succeeded
            ? successMessage
            : $"{result.SucceededCount} message action(s) succeeded; {result.FailedCount} failed.";
        return result;
    }
}