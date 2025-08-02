namespace Mailozaurr;

public interface ISentMessageRepository {
    Task SaveAsync(SentMessageRecord record, CancellationToken cancellationToken = default);
    Task<SentMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default);
}
