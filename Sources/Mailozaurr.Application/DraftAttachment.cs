namespace Mailozaurr.Application;

/// <summary>
/// Represents an attachment that should be included when composing a draft.
/// </summary>
public sealed class DraftAttachment {
    /// <summary>Absolute or relative source path.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Optional explicit attachment name override.</summary>
    public string? FileName { get; set; }

    /// <summary>Optional explicit content type.</summary>
    public string? ContentType { get; set; }

    /// <summary>Whether the attachment should be embedded inline.</summary>
    public bool IsInline { get; set; }

    /// <summary>Content id used for inline attachments.</summary>
    public string? ContentId { get; set; }
}