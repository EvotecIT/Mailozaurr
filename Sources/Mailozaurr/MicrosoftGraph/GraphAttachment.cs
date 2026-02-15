namespace Mailozaurr;

/// <summary>
/// Represents a simple file attachment used when sending messages.
/// </summary>
/// <remarks>
/// This is the lightweight counterpart to <c>AttachmentItem</c>
/// used during message creation.
/// </remarks>
public class GraphAttachment {
    /// <summary>
    /// Gets or sets the Graph type of the attachment.
    /// </summary>
    [JsonPropertyName("@odata.type")]
    public string ODataType { get; set; } = "#microsoft.graph.fileAttachment";

    /// <summary>
    /// Gets or sets the attachment file name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets attachment content type.
    /// </summary>
    [JsonPropertyName("contentType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the file content encoded as a Base64 string.
    /// </summary>
    [JsonPropertyName("contentBytes")]
    public string ContentBytes { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether this attachment should be rendered inline in the
    /// message body.
    /// </summary>
    [JsonPropertyName("isInline")]
    public bool IsInline { get; set; }

    /// <summary>
    /// Optional identifier used to reference the attachment via a <c>cid:</c>
    /// URL within the HTML body.
    /// </summary>
    [JsonPropertyName("contentId")]
    public string? ContentId { get; set; }

    /// <summary>
    /// Creates a <see cref="GraphAttachment"/> from a local file path.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <returns>The created attachment.</returns>
    public static GraphAttachment FromFile(string filePath) {
        var fileInfo = new FileInfo(filePath);
        var fileBytes = File.ReadAllBytes(filePath);
        var fileContentBase64 = Convert.ToBase64String(fileBytes);

        return new GraphAttachment {
            Name = fileInfo.Name,
            ContentBytes = fileContentBase64
        };
    }
}
