namespace Mailozaurr.Application;

/// <summary>
/// Represents the outcome of processing queued outbound messages.
/// </summary>
public sealed class QueueProcessResult : OperationResult {
    /// <summary>Messages skipped before delivery was attempted.</summary>
    public int SkippedCount { get; set; }

    /// <summary>Messages for which a delivery attempt started.</summary>
    public int AttemptedCount { get; set; }

    /// <summary>Messages successfully sent.</summary>
    public int SentCount { get; set; }

    /// <summary>Messages whose current attempt failed.</summary>
    public int FailedCount { get; set; }

    /// <summary>Messages removed from the queue without being delivered.</summary>
    public int DroppedCount { get; set; }
}