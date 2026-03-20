using System;

namespace Mailozaurr;

/// <summary>
/// Provides telemetry hooks for <see cref="PendingMessageProcessor"/> operations.
/// </summary>
public interface IPendingMessageProcessorObserver {
    /// <summary>Called when a pending message is skipped before attempting delivery.</summary>
    void MessageSkipped(PendingMessageRecord record, PendingMessageSkipReason reason);

    /// <summary>Called immediately before a delivery attempt is made.</summary>
    void MessageAttemptStarted(PendingMessageRecord record, int attempt);

    /// <summary>Called when a message is successfully sent.</summary>
    void MessageSent(PendingMessageRecord record, int attempt, TimeSpan duration);

    /// <summary>
    /// Called when a delivery attempt fails.
    /// </summary>
    /// <param name="record">The record that was processed.</param>
    /// <param name="attempt">The attempt number that failed.</param>
    /// <param name="exception">The exception describing the failure.</param>
    /// <param name="duration">How long the attempt took.</param>
    /// <param name="willRetry">Indicates whether another attempt will be scheduled.</param>
    /// <param name="retryDelay">Delay until the next attempt if <paramref name="willRetry"/> is <see langword="true"/>.</param>
    void MessageFailed(
        PendingMessageRecord record,
        int attempt,
        Exception exception,
        TimeSpan duration,
        bool willRetry,
        TimeSpan? retryDelay);

    /// <summary>Called when a message is removed from the pending queue without being sent.</summary>
    void MessageDropped(
        PendingMessageRecord record,
        int attempt,
        PendingMessageDropReason reason,
        Exception? exception);
}

/// <summary>
/// Indicates why a pending message was skipped.
/// </summary>
public enum PendingMessageSkipReason {
    /// <summary>The record does not specify a valid message identifier.</summary>
    MissingMessageId,

    /// <summary>The record is scheduled for a future attempt.</summary>
    NotDue,

    /// <summary>The record was already leased by another processor.</summary>
    LeaseNotAcquired
}

/// <summary>
/// Indicates why a pending message was removed from the queue.
/// </summary>
public enum PendingMessageDropReason {
    /// <summary>The message exceeded the configured retry limit.</summary>
    RetryLimitReached,

    /// <summary>The sender reported a permanent failure.</summary>
    PermanentFailure
}
