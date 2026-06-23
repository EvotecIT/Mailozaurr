namespace Mailozaurr.Application;

/// <summary>
/// Represents normalized metadata for a queued outbound message.
/// </summary>
public sealed class QueuedMessageSummary {
    /// <summary>Stable queued message identifier.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Underlying transport/provider recorded for the message.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Normalized profile kind inferred from the queued provider.</summary>
    public MailProfileKind ProfileKind { get; set; } = MailProfileKind.Unknown;

    /// <summary>When the message was first queued.</summary>
    public DateTimeOffset QueuedAt { get; set; }

    /// <summary>When the next delivery attempt is scheduled.</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>Number of delivery attempts already performed.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Whether provider-specific metadata is present.</summary>
    public bool HasProviderData { get; set; }

    /// <summary>Whether this projection came from dead-letter storage.</summary>
    public bool IsDeadLetter { get; set; }

    /// <summary>Terminal drop reason when this is a dead-letter record.</summary>
    public string? DeadLetterReason { get; set; }

    /// <summary>Terminal error message when this is a dead-letter record.</summary>
    public string? ErrorMessage { get; set; }
}
