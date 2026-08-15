namespace Mailozaurr;

/// <summary>
/// Handles normalized send operations for a specific profile kind.
/// </summary>
public interface IMailSendHandler {
    /// <summary>Profile kind handled by this instance.</summary>
    MailProfileKind Kind { get; }

    /// <summary>Sends or queues the message for the provided profile.</summary>
    Task<SendResult> SendAsync(MailProfile profile, SendMessageRequest request, CancellationToken cancellationToken = default);
}