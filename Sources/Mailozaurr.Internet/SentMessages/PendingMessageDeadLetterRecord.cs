using System;

namespace Mailozaurr;

/// <summary>
/// Captures a pending message that was removed from active retry processing.
/// </summary>
public sealed class PendingMessageDeadLetterRecord {
    /// <summary>Original queued message.</summary>
    public PendingMessageRecord Message { get; set; } = new();

    /// <summary>Reason the message was quarantined.</summary>
    public PendingMessageDropReason Reason { get; set; }

    /// <summary>Attempt number associated with the terminal outcome.</summary>
    public int Attempt { get; set; }

    /// <summary>When the message was moved to dead letter storage.</summary>
    public DateTimeOffset DeadLetteredAt { get; set; }

    /// <summary>Exception type captured from the terminal failure, when available.</summary>
    public string? ExceptionType { get; set; }

    /// <summary>Terminal error message, when available.</summary>
    public string? ErrorMessage { get; set; }
}
