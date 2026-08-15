namespace Mailozaurr;

/// <summary>
/// Per-plan dry-run preview entry for a transformed stored action plan.
/// </summary>
public sealed class MessageActionPlanBatchTransformPreviewItem {
    /// <summary>Zero-based plan index in the source batch.</summary>
    public int Index { get; set; }

    /// <summary>Action name such as move, delete, or mark-read.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Normalized execution kind.</summary>
    public string ExecutionKind { get; set; } = string.Empty;

    /// <summary>Original profile identifier.</summary>
    public string SourceProfileId { get; set; } = string.Empty;

    /// <summary>Transformed profile identifier.</summary>
    public string TargetProfileId { get; set; } = string.Empty;

    /// <summary>Original mailbox identifier.</summary>
    public string? SourceMailboxId { get; set; }

    /// <summary>Transformed mailbox identifier.</summary>
    public string? TargetMailboxId { get; set; }

    /// <summary>Original source folder identifier.</summary>
    public string? SourceFolderId { get; set; }

    /// <summary>Transformed source folder identifier.</summary>
    public string? TargetFolderId { get; set; }

    /// <summary>Original requested destination folder for move-like plans.</summary>
    public string? SourceDestinationFolderId { get; set; }

    /// <summary>Transformed requested destination folder for move-like plans.</summary>
    public string? TargetDestinationFolderId { get; set; }

    /// <summary>Whether any effective plan values would change.</summary>
    public bool WillChange { get; set; }

    /// <summary>Whether the confirmation token would be regenerated.</summary>
    public bool ConfirmationTokenWillChange { get; set; }

    /// <summary>Human-readable summary of the effective transform.</summary>
    public string Summary { get; set; } = string.Empty;
}