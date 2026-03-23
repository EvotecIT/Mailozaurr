namespace Mailozaurr.Application;

/// <summary>
/// Persists reusable message action plan batches.
/// </summary>
public interface IMailMessageActionPlanBatchStore {
    /// <summary>Lists all saved plan batches.</summary>
    Task<IReadOnlyList<MailMessageActionPlanBatch>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a saved plan batch by id.</summary>
    Task<MailMessageActionPlanBatch?> GetByIdAsync(string batchId, CancellationToken cancellationToken = default);

    /// <summary>Saves or updates a plan batch.</summary>
    Task SaveAsync(MailMessageActionPlanBatch batch, CancellationToken cancellationToken = default);

    /// <summary>Removes a saved plan batch by id.</summary>
    Task<bool> RemoveAsync(string batchId, CancellationToken cancellationToken = default);
}
