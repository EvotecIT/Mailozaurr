namespace Mailozaurr;

/// <summary>
/// Metadata describing an attachment for upload.
/// </summary>
public class GraphAttachmentItem {
    [JsonPropertyName("attachmentType")]
    public string AttachmentType { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>Creates a new attachment item.</summary>
    /// <param name="attachmentType">Type of the attachment.</param>
    /// <param name="name">File name.</param>
    /// <param name="size">File size.</param>
    public GraphAttachmentItem(string attachmentType, string name, long size) {
        AttachmentType = attachmentType;
        Name = name;
        Size = size;
    }
}
