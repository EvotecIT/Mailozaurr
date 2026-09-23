using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Persists pending messages that reached a terminal, inspectable failure state.
/// </summary>
public interface IPendingMessageDeadLetterRepository {
    /// <summary>
    /// Saves a terminal pending-message outcome. Repeating the same original
    /// message id must be idempotent so an interrupted queue transition can resume.
    /// </summary>
    Task SaveAsync(PendingMessageDeadLetterRecord record, CancellationToken cancellationToken = default);

    /// <summary>Gets a dead-lettered message by original message id.</summary>
    Task<PendingMessageDeadLetterRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>Enumerates dead-lettered messages.</summary>
    IAsyncEnumerable<PendingMessageDeadLetterRecord> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes a dead-lettered message by original message id.</summary>
    Task RemoveAsync(string messageId, CancellationToken cancellationToken = default);
}
