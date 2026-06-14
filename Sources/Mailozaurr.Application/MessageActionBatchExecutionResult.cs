namespace Mailozaurr.Application;

/// <summary>
/// Aggregate outcome for executing a batch of normalized message action plans.
/// </summary>
public sealed class MessageActionBatchExecutionResult : OperationResult {
    /// <summary>Total number of plans submitted for execution.</summary>
    public int RequestedPlanCount { get; set; }

    /// <summary>Total number of plans that were actually attempted.</summary>
    public int AttemptedPlanCount { get; set; }

    /// <summary>Total number of plans that completed successfully.</summary>
    public int SucceededPlanCount { get; set; }

    /// <summary>Total number of plans that failed.</summary>
    public int FailedPlanCount { get; set; }

    /// <summary>Total number of plans skipped after an earlier failure.</summary>
    public int SkippedPlanCount { get; set; }

    /// <summary>Per-plan execution outcomes in request order.</summary>
    public List<MessageActionBatchExecutionItemResult> Results { get; set; } = new();
}