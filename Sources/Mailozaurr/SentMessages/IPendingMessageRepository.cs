namespace Mailozaurr;

public interface IPendingMessageRepository {
    Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default);
    Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<PendingMessageRecord> GetAllAsync(CancellationToken cancellationToken = default);
    Task RemoveAsync(string messageId, CancellationToken cancellationToken = default);
}
