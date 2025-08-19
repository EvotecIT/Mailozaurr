namespace Mailozaurr;

/// <summary>
/// Abstraction for storing and retrieving sent message records.
/// </summary>
public interface ISentMessageRepository {
    /// <summary>Saves a sent message record.</summary>
    /// <param name="record">Record to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task SaveAsync(SentMessageRecord record, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a sent message record by its message identifier.</summary>
    /// <param name="messageId">Identifier of the message.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The stored record or <see langword="null" /> if not found.</returns>
    Task<SentMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default);
}
