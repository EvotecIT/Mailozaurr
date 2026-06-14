namespace Mailozaurr.Application;

/// <summary>
/// Dry-run preview for cloning and transforming a stored message-action plan batch.
/// </summary>
public sealed class MailMessageActionPlanBatchTransformPreview : OperationResult {
    /// <summary>Source batch identifier.</summary>
    public string SourceBatchId { get; set; } = string.Empty;

    /// <summary>Source batch name when available.</summary>
    public string? SourceBatchName { get; set; }

    /// <summary>Total plans contained in the source batch.</summary>
    public int PlanCount { get; set; }

    /// <summary>Total transformed plans whose effective values would change.</summary>
    public int ChangedPlanCount { get; set; }

    /// <summary>Total transformed plans whose confirmation token would be regenerated.</summary>
    public int ConfirmationTokenChangedCount { get; set; }

    /// <summary>Whether the requested target profile exists when a profile remap was requested.</summary>
    public bool? TargetProfileExists { get; set; }

    /// <summary>Target profile kind when a profile remap was requested and resolved.</summary>
    public MailProfileKind? TargetProfileKind { get; set; }

    /// <summary>Validation or transform warnings.</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Validation errors that would block saving the transformed clone.</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Per-plan transform preview items.</summary>
    public List<MessageActionPlanBatchTransformPreviewItem> Plans { get; set; } = new();
}