namespace Mailozaurr.Application;

/// <summary>
/// Represents normalized attachment metadata.
/// </summary>
public sealed class AttachmentSummary {
    /// <summary>Owning message identifier.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Provider-specific attachment identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Attachment file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Attachment content type.</summary>
    public string? ContentType { get; set; }

    /// <summary>Attachment size in bytes when known.</summary>
    public long? SizeInBytes { get; set; }

    /// <summary>Whether the attachment is inline content.</summary>
    public bool IsInline { get; set; }

    /// <summary>Inline content id when applicable.</summary>
    public string? ContentId { get; set; }
}
