namespace Mailozaurr.Application;

/// <summary>
/// Lightweight projection of a queued outbound message for list and agent scenarios.
/// </summary>
public sealed class QueuedMessageCompact {
    /// <summary>Stable queued message identifier.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Underlying transport/provider recorded for the message.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Normalized profile kind inferred from the queued provider.</summary>
    public MailProfileKind ProfileKind { get; set; } = MailProfileKind.Unknown;

    /// <summary>When the next delivery attempt is scheduled.</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>Number of delivery attempts already performed.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Whether the queued message is currently due for processing.</summary>
    public bool IsDue { get; set; }

    /// <summary>Whether provider-specific metadata is present.</summary>
    public bool HasProviderData { get; set; }

    /// <summary>Short human-readable summary line.</summary>
    public string Summary { get; set; } = string.Empty;
}