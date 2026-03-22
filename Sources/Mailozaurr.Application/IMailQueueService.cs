namespace Mailozaurr.Application;

/// <summary>
/// Provides reusable access to queued outbound messages.
/// </summary>
public interface IMailQueueService {
    /// <summary>
    /// Lists queued messages.
    /// </summary>
    Task<IReadOnlyList<QueuedMessageSummary>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists queued messages using a lightweight projection.
    /// </summary>
    Task<IReadOnlyList<QueuedMessageCompact>> ListCompactAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a queued message by its message identifier.
    /// </summary>
    Task<QueuedMessageSummary?> GetAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a queued message by its message identifier using a lightweight projection.
    /// </summary>
    Task<QueuedMessageCompact?> GetCompactAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a queued message.
    /// </summary>
    Task<OperationResult> RemoveAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes all due queued messages.
    /// </summary>
    Task<QueueProcessResult> ProcessAsync(CancellationToken cancellationToken = default);
}
