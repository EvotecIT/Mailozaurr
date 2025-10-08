namespace Mailozaurr;

/// <summary>
/// Abstraction for persisting and retrieving queued email messages.
/// </summary>
public interface IPendingMessageRepository {
    /// <summary>Saves a pending message record.</summary>
    /// <param name="record">The record to save.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default);

    /// <summary>Gets a pending message by its unique message id.</summary>
    /// <param name="messageId">The message id to search for.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The pending message record or <c>null</c> if not found.</returns>
    Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>Enumerates all pending message records.</summary>
    /// <param name="cancellationToken">Token used to cancel the enumeration.</param>
    /// <returns>Asynchronous sequence of pending message records.</returns>
    IAsyncEnumerable<PendingMessageRecord> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes a message from the repository by id.</summary>
    /// <param name="messageId">The message id to remove.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task RemoveAsync(string messageId, CancellationToken cancellationToken = default);
}
