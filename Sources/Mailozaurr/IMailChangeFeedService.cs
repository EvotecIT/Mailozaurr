namespace Mailozaurr;

/// <summary>Reads provider changes through a normalized mailbox change-feed contract.</summary>
public interface IMailChangeFeedService {
    /// <summary>Reads the next durable Graph delta or Gmail history batch.</summary>
    Task<MailChangeFeedResult> GetChangesAsync(
        MailChangeFeedRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Waits for an ephemeral IMAP IDLE batch.</summary>
    Task<MailChangeFeedResult> WaitForChangesAsync(
        MailChangeWaitRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Creates or refreshes a provider notification subscription.</summary>
    Task<MailChangeSubscriptionResult> SubscribeAsync(
        MailChangeSubscriptionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a provider notification subscription.</summary>
    Task<MailChangeSubscriptionResult> UnsubscribeAsync(
        MailChangeUnsubscribeRequest request,
        CancellationToken cancellationToken = default);
}
