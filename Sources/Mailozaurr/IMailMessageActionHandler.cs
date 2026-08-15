namespace Mailozaurr;

/// <summary>
/// Handles normalized mailbox message actions for a specific profile kind.
/// </summary>
public interface IMailMessageActionHandler {
    /// <summary>Profile kind handled by this instance.</summary>
    MailProfileKind Kind { get; }

    /// <summary>Sets read/unread state for messages.</summary>
    Task<MessageActionResult> SetReadStateAsync(MailProfile profile, SetReadStateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sets flagged/starred state for messages.</summary>
    Task<MessageActionResult> SetFlaggedStateAsync(MailProfile profile, SetFlaggedStateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Moves messages to a destination folder.</summary>
    Task<MessageActionResult> MoveAsync(MailProfile profile, MoveMessagesRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes messages.</summary>
    Task<MessageActionResult> DeleteAsync(MailProfile profile, DeleteMessagesRequest request, CancellationToken cancellationToken = default);
}