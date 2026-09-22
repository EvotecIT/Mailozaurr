namespace Mailozaurr;

/// <summary>
/// Optional repository contract for committing a send outcome only while the
/// caller still owns the processing lease.
/// </summary>
public interface IPendingMessageLeaseCommitter {
    /// <summary>Saves the record only if its processing lease still belongs to this worker.</summary>
    Task<bool> TrySaveWithLeaseAsync(PendingMessageRecord record, string leaseId,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the record only if its processing lease still belongs to this worker.</summary>
    Task<bool> TryRemoveWithLeaseAsync(string messageId, string leaseId,
        CancellationToken cancellationToken = default);
}
