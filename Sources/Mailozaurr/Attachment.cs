namespace Mailozaurr;

/// <summary>
/// Represents a file attachment from Microsoft Graph.
/// </summary>
public class Attachment {
    /// <summary>
    /// Gets or sets the attachment file name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the Base64-encoded file content.
    /// </summary>
    public string ContentBytes { get; set; }
    // Add more properties as needed
}
