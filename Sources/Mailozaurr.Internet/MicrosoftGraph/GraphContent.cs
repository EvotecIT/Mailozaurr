namespace Mailozaurr;

/// <summary>
/// Body content for a message.
/// </summary>
public class GraphContent {
    /// <summary>
    /// Gets or sets the content type, such as "Text" or "HTML".
    /// </summary>
    [JsonPropertyName("contentType")]
    public string Type { get; set; } = "Text";

    /// <summary>
    /// Gets or sets the content value.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}