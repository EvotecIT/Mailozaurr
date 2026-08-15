namespace Mailozaurr;

/// <summary>
/// Provides reusable lifecycle operations for persisted message action plan batches.
/// </summary>
public interface IMailMessageActionPlanRegistryService {
    /// <summary>Lists all saved plan batches.</summary>
    Task<IReadOnlyList<MailMessageActionPlanBatch>> GetBatchesAsync(MailMessageActionPlanBatchQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>Lists all saved plan batches using a lightweight projection.</summary>
    Task<IReadOnlyList<MailMessageActionPlanBatchCompact>> GetBatchesCompactAsync(MailMessageActionPlanBatchQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>Lists all saved plan batches using a richer summary projection.</summary>
    Task<IReadOnlyList<MailMessageActionPlanBatchSummary>> GetBatchesSummaryAsync(MailMessageActionPlanBatchQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>Gets a batch by id.</summary>
    Task<MailMessageActionPlanBatch?> GetBatchAsync(string batchId, CancellationToken cancellationToken = default);

    /// <summary>Gets a batch by id using a lightweight projection.</summary>
    Task<MailMessageActionPlanBatchCompact?> GetBatchCompactAsync(string batchId, CancellationToken cancellationToken = default);

    /// <summary>Gets a batch by id using a richer summary projection.</summary>
    Task<MailMessageActionPlanBatchSummary?> GetBatchSummaryAsync(string batchId, CancellationToken cancellationToken = default);

    /// <summary>Saves or updates a persisted plan batch.</summary>
    Task<OperationResult> SaveAsync(MailMessageActionPlanBatch batch, CancellationToken cancellationToken = default);

    /// <summary>Builds and saves a persisted plan batch from a common mailbox action selection.</summary>
    Task<OperationResult> CreateCommonBatchAsync(
        string batchId,
        string name,
        CommonMessageActionsPreviewRequest request,
        IReadOnlyList<string>? actions = null,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>Builds and saves a persisted plan batch from an existing common mailbox action preview bundle.</summary>
    Task<OperationResult> CreateCommonBatchFromPreviewAsync(
        string batchId,
        string name,
        CommonMessageActionsPreview preview,
        IReadOnlyList<string>? actions = null,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>Clones an existing persisted batch to a new identifier and name.</summary>
    Task<OperationResult> CloneAsync(string sourceBatchId, string targetBatchId, string name, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>Clones an existing persisted batch while applying shared profile/mailbox/folder/destination transforms.</summary>
    Task<OperationResult> TransformCloneAsync(
        string sourceBatchId,
        string targetBatchId,
        string name,
        MessageActionPlanBatchTransformRequest transform,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>Previews a stored batch transform and reports what would change before cloning and saving it.</summary>
    Task<MailMessageActionPlanBatchTransformPreview> PreviewTransformCloneAsync(
        string sourceBatchId,
        MessageActionPlanBatchTransformRequest transform,
        CancellationToken cancellationToken = default);

    /// <summary>Appends one normalized plan to an existing persisted batch.</summary>
    Task<OperationResult> AppendPlanAsync(string batchId, MessageActionExecutionPlan plan, CancellationToken cancellationToken = default);

    /// <summary>Loads one normalized plan from a file and appends it to an existing persisted batch.</summary>
    Task<OperationResult> AppendImportedPlanAsync(string batchId, string path, CancellationToken cancellationToken = default);

    /// <summary>Replaces one plan inside an existing persisted batch by zero-based index.</summary>
    Task<OperationResult> ReplacePlanAtAsync(string batchId, int index, MessageActionExecutionPlan plan, CancellationToken cancellationToken = default);

    /// <summary>Loads one normalized plan from a file and replaces a stored plan by zero-based index.</summary>
    Task<OperationResult> ReplaceImportedPlanAtAsync(string batchId, int index, string path, CancellationToken cancellationToken = default);

    /// <summary>Removes one plan from an existing persisted batch by zero-based index.</summary>
    Task<OperationResult> RemovePlanAtAsync(string batchId, int index, CancellationToken cancellationToken = default);

    /// <summary>Deletes a persisted plan batch by id.</summary>
    Task<OperationResult> DeleteAsync(string batchId, CancellationToken cancellationToken = default);

    /// <summary>Imports a persisted plan batch from an external batch file.</summary>
    Task<OperationResult> ImportAsync(string batchId, string name, string path, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>Exports a persisted plan batch to an external batch file.</summary>
    Task<OperationResult> ExportAsync(string batchId, string path, CancellationToken cancellationToken = default);

    /// <summary>Executes a persisted plan batch through the shared batch executor.</summary>
    Task<MessageActionBatchExecutionResult> ExecuteAsync(
        string batchId,
        bool continueOnError = true,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? confirmationTokens = null);
}
