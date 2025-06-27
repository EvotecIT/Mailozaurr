namespace Mailozaurr;

/// <summary>
/// Represents a simple file attachment used when sending messages.
/// </summary>
public class GraphAttachment {
    [JsonPropertyName("@odata.type")]
    public string ODataType { get; set; } = "#microsoft.graph.fileAttachment";

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("contentBytes")]
    public string ContentBytes { get; set; }

    [JsonPropertyName("isInline")]
    /// <summary>
    /// Indicates whether this attachment should be rendered inline in the
    /// message body.
    /// </summary>
    public bool IsInline { get; set; }

    [JsonPropertyName("contentId")]
    /// <summary>
    /// Optional identifier used to reference the attachment via a <c>cid:</c>
    /// URL within the HTML body.
    /// </summary>
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