namespace Mailozaurr;

/// <summary>
/// Represents a file attachment from Microsoft Graph.
/// </summary>
public class Attachment {
    /// <summary>
    /// Gets or sets the file name of the attachment.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the attachment content encoded as a Base64 string.
    /// </summary>
    public string ContentBytes { get; set; }
    // Add more properties as needed
}
