namespace Mailozaurr;

/// <summary>
/// Aggregate dry-run result comparing common mailbox actions for the same message selection.
/// </summary>
public sealed class CommonMessageActionsPreview : OperationResult {
    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Owning mailbox identifier when relevant.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional source folder identifier.</summary>
    public string? FolderId { get; set; }

    /// <summary>Optional custom destination folder value included in the comparison.</summary>
    public string? RequestedDestinationFolderId { get; set; }

    /// <summary>Total raw message identifiers provided in the request.</summary>
    public int RequestedCount { get; set; }

    /// <summary>Total unique, non-empty message identifiers after normalization.</summary>
    public int UniqueMessageCount { get; set; }

    /// <summary>Total duplicate or empty message identifiers removed during normalization.</summary>
    public int DuplicateOrEmptyCount { get; set; }

    /// <summary>The normalized unique message identifiers that would be acted on.</summary>
    public List<string> MessageIds { get; set; } = new();

    /// <summary>Total number of action previews included in the bundle.</summary>
    public int IncludedActionCount { get; set; }

    /// <summary>Total number of action previews that are supported and ready.</summary>
    public int SucceededActionCount { get; set; }

    /// <summary>Total number of action previews that are unsupported or otherwise blocked.</summary>
    public int FailedActionCount { get; set; }

    /// <summary>Top-level warnings detected during preview normalization.</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Included action previews such as read-state, flagged-state, archive, trash, move, and delete.</summary>
    public List<MessageActionPreviewItem> Actions { get; set; } = new();
}