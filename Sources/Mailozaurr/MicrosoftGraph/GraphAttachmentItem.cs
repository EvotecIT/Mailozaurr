namespace Mailozaurr;

/// <summary>
/// Metadata describing an attachment for upload.
/// </summary>
/// <remarks>
/// This type mirrors the <c>attachmentItem</c> resource used
/// when creating an upload session in the Graph API.
/// </remarks>
public class GraphAttachmentItem {
    /// <summary>
    /// Gets or sets the type of the attachment being uploaded.
    /// </summary>
    [JsonPropertyName("attachmentType")]
    public string AttachmentType { get; set; }

    /// <summary>
    /// Gets or sets the file name of the attachment.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the size of the attachment in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets optional content type.
    /// </summary>
    [JsonPropertyName("contentType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets whether attachment should be rendered inline.
    /// </summary>
    [JsonPropertyName("isInline")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsInline { get; set; }

    /// <summary>
    /// Gets or sets optional content identifier for inline attachments.
    /// </summary>
    [JsonPropertyName("contentId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentId { get; set; }

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
