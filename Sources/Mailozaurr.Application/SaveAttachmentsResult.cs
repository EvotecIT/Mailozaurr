namespace Mailozaurr.Application;

/// <summary>
/// Represents the outcome of saving multiple attachments from a single message.
/// </summary>
public sealed class SaveAttachmentsResult : OperationResult {
    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Owning message identifier.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Total attachments matched for processing.</summary>
    public int MatchedCount { get; set; }

    /// <summary>Total save attempts performed.</summary>
    public int AttemptedCount { get; set; }

    /// <summary>Total attachments saved successfully.</summary>
    public int SavedCount { get; set; }

    /// <summary>Total attachments whose save attempt failed.</summary>
    public int FailedCount { get; set; }

    /// <summary>Per-attachment outcomes.</summary>
    public List<SavedAttachmentResult> Results { get; set; } = new();
}
