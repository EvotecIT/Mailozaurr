namespace Mailozaurr;

/// <summary>
/// Body content for a message.
/// </summary>
public class GraphContent {
    [JsonPropertyName("contentType")]
    public string Type { get; set; } = "Text";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}
