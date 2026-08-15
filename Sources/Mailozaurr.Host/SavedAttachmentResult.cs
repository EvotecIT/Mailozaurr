namespace Mailozaurr.Hosting;

/// <summary>
/// Outcome of saving a single attachment as part of a batch attachment operation.
/// </summary>
public sealed class SavedAttachmentResult : OperationResult {
    /// <summary>Provider-specific attachment identifier.</summary>
    public string AttachmentId { get; set; } = string.Empty;

    /// <summary>Attachment file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Attachment content type when known.</summary>
    public string? ContentType { get; set; }
}