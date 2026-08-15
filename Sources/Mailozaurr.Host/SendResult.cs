namespace Mailozaurr.Hosting;

/// <summary>
/// Represents the result of a normalized send or queue operation.
/// </summary>
public sealed class SendResult : OperationResult {
    /// <summary>Profile that was used for the send attempt.</summary>
    public string? ProfileId { get; set; }

    /// <summary>Profile kind that handled the operation.</summary>
    public MailProfileKind ProfileKind { get; set; } = MailProfileKind.Unknown;

    /// <summary>Whether the message was queued instead of sent immediately.</summary>
    public bool Queued { get; set; }

    /// <summary>Queued message identifier when the message was enqueued.</summary>
    public string? QueueMessageId { get; set; }

    /// <summary>Provider-native message identifier when available.</summary>
    public string? ProviderMessageId { get; set; }
}