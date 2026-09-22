namespace Mailozaurr;

/// <summary>
/// Optional repository contract for extending a processing lease while a
/// provider request is still in flight.
/// </summary>
public interface IPendingMessageLeaseRenewer {
    /// <summary>
    /// Extends the lease only when the record still has the expected expiry
    /// and remains owned by the same worker.
    /// </summary>
    Task<bool> TryRenewLeaseAsync(string messageId, string leaseId, DateTimeOffset expectedLeaseUntil,
        DateTimeOffset newLeaseUntil, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional renewal contract that returns the committed expiry when storage
/// contention extends the requested lease timestamp.
/// </summary>
public interface IPendingMessageLeaseExpirationRenewer : IPendingMessageLeaseRenewer {
    /// <summary>Renews an owned lease and returns its committed expiry, or null when ownership was lost.</summary>
    Task<DateTimeOffset?> TryRenewLeaseAndGetExpirationAsync(string messageId, string leaseId,
        DateTimeOffset expectedLeaseUntil, DateTimeOffset newLeaseUntil,
        CancellationToken cancellationToken = default);
}
