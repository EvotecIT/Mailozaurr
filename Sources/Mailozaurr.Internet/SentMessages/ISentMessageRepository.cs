namespace Mailozaurr;

/// <summary>
/// Abstraction for persisting and retrieving messages that were successfully sent.
/// </summary>
public interface ISentMessageRepository {
    /// <summary>Saves a sent message record.</summary>
    /// <param name="record">The record to save.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task SaveAsync(SentMessageRecord record, CancellationToken cancellationToken = default);

    /// <summary>Gets a sent message by its unique message id.</summary>
    /// <param name="messageId">The message id to search for.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The sent message record or <c>null</c> if not found.</returns>
    Task<SentMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default);
}