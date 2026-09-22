namespace Mailozaurr;

/// <summary>
/// Optional repository contract for extending a processing lease while a
/// provider request is still in flight.
/// </summary>
public interface IPendingMessageLeaseRenewer {
    /// <summary>
    /// Extends the lease only when the record still has the expected expiry
    /// and has not been marked as accepted by its provider.
    /// </summary>
    Task<bool> TryRenewLeaseAsync(string messageId, string leaseId, DateTimeOffset expectedLeaseUntil,
        DateTimeOffset newLeaseUntil, CancellationToken cancellationToken = default);
}
