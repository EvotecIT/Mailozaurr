namespace Mailozaurr.Application;

/// <summary>
/// Imports and exports normalized message action plans to stable external files.
/// </summary>
public interface IMailMessageActionPlanExchangeService {
    /// <summary>Loads one normalized action plan from an external file.</summary>
    Task<MessageActionExecutionPlan> LoadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Saves one normalized action plan to an external file.</summary>
    Task SaveAsync(string path, MessageActionExecutionPlan plan, CancellationToken cancellationToken = default);

    /// <summary>Loads a batch of normalized action plans from an external file.</summary>
    Task<IReadOnlyList<MessageActionExecutionPlan>> LoadBatchAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Saves a batch of normalized action plans to an external file.</summary>
    Task SaveBatchAsync(string path, IReadOnlyList<MessageActionExecutionPlan> plans, CancellationToken cancellationToken = default);
}
