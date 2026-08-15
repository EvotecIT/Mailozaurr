namespace Mailozaurr.Hosting;

/// <summary>
/// Provides normalized mailbox message actions such as read-state changes, move, and delete.
/// </summary>
public interface IMailMessageActionService {
    /// <summary>Sets read/unread state for messages.</summary>
    Task<MessageActionResult> SetReadStateAsync(SetReadStateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sets flagged/starred state for messages.</summary>
    Task<MessageActionResult> SetFlaggedStateAsync(SetFlaggedStateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Moves messages to a destination folder.</summary>
    Task<MessageActionResult> MoveAsync(MoveMessagesRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes messages.</summary>
    Task<MessageActionResult> DeleteAsync(DeleteMessagesRequest request, CancellationToken cancellationToken = default);
}