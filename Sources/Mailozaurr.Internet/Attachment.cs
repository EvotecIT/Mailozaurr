namespace Mailozaurr;

/// <summary>
/// Represents a file attachment from Microsoft Graph.
/// </summary>
/// <remarks>
/// Only basic metadata required for upload is exposed.
/// </remarks>
public class Attachment {
    /// <summary>
    /// Gets or sets the file name of the attachment.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the attachment content encoded as a Base64 string.
    /// </summary>
    public string ContentBytes { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size of the attachment in bytes.
    /// </summary>
    public long Size { get; set; }
    // Add more properties as needed
}