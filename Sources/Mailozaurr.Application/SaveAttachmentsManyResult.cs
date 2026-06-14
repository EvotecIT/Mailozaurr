namespace Mailozaurr.Application;

/// <summary>
/// Represents the outcome of saving attachments across multiple messages.
/// </summary>
public sealed class SaveAttachmentsManyResult : OperationResult {
    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Total messages requested for inspection.</summary>
    public int RequestedMessageCount { get; set; }

    /// <summary>Total message-level save operations attempted.</summary>
    public int AttemptedMessageCount { get; set; }

    /// <summary>Total messages that completed without attachment-save failures.</summary>
    public int SucceededMessageCount { get; set; }

    /// <summary>Total messages whose attachment-save operation failed.</summary>
    public int FailedMessageCount { get; set; }

    /// <summary>Total attachments matched for processing across all messages.</summary>
    public int MatchedCount { get; set; }

    /// <summary>Total attachment save attempts across all messages.</summary>
    public int AttemptedCount { get; set; }

    /// <summary>Total attachments saved successfully across all messages.</summary>
    public int SavedCount { get; set; }

    /// <summary>Total attachments whose save attempt failed across all messages.</summary>
    public int FailedCount { get; set; }

    /// <summary>Per-message outcomes.</summary>
    public List<SaveAttachmentsResult> MessageResults { get; set; } = new();
}