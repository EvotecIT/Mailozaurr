namespace Mailozaurr;

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

    /// <summary>SMTP host that accepted the message, or the last attempted host on failure.</summary>
    public string? Server { get; set; }

    /// <summary>SMTP port of the final attempt.</summary>
    public int? Port { get; set; }

    /// <summary>Ordered SMTP delivery attempts, including transient failures before acceptance.</summary>
    public IReadOnlyList<SmtpDeliveryAttempt> Attempts { get; set; } = Array.Empty<SmtpDeliveryAttempt>();

    /// <summary>Whether the final failure is eligible for a later retry.</summary>
    public bool IsTransient { get; set; }

    /// <summary>Whether any attempt may have delivered without a confirmed acknowledgement.</summary>
    public bool DeliveryUnknown { get; set; }
}
