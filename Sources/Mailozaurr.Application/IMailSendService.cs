namespace Mailozaurr.Application;

/// <summary>
/// Provides normalized send-oriented mailbox operations.
/// </summary>
public interface IMailSendService {
    /// <summary>Sends or queues a message based on the request.</summary>
    Task<SendResult> SendAsync(SendMessageRequest request, CancellationToken cancellationToken = default);
}
