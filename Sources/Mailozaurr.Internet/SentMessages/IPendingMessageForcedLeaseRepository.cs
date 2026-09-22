namespace Mailozaurr;

/// <summary>Optional repository contract for processing a scheduled retry now without stealing an active lease.</summary>
public interface IPendingMessageForcedLeaseRepository {
    /// <summary>Acquires a lease regardless of the retry schedule when no other worker holds it.</summary>
    Task<PendingMessageRecord?> TryAcquireForcedLeaseAsync(string messageId,
        DateTimeOffset now, DateTimeOffset leaseUntil, CancellationToken cancellationToken = default);
}
