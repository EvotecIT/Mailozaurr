namespace Mailozaurr;

/// <summary>
/// Metadata about a Gmail message attachment.
/// </summary>
public sealed class GmailAttachmentInfo {
    /// <summary>Attachment identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Attachment file name.</summary>
    public string? FileName { get; set; }

    /// <summary>MIME type of the attachment.</summary>
    public string? MimeType { get; set; }
}