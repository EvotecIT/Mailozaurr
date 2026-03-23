namespace Mailozaurr.Application;

/// <summary>
/// Execution outcome for one normalized message action plan inside a batch.
/// </summary>
public sealed class MessageActionBatchExecutionItemResult : OperationResult {
    /// <summary>Zero-based plan index inside the batch request.</summary>
    public int Index { get; set; }

    /// <summary>Stable action name such as mark-read, archive, move, or delete.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Normalized execution kind such as SetReadState, Move, or Delete.</summary>
    public string ExecutionKind { get; set; } = string.Empty;

    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Total message identifiers covered by this plan.</summary>
    public int RequestedCount { get; set; }

    /// <summary>Total message identifiers that succeeded for this plan.</summary>
    public int SucceededCount { get; set; }

    /// <summary>Total message identifiers that failed for this plan.</summary>
    public int FailedCount { get; set; }
}
