namespace Mailozaurr;

/// <summary>
/// Wrapper used when serializing a message for Graph API calls.
/// </summary>
public class GraphMessageContainer {
    [JsonPropertyName("message")]
    public GraphMessage Message { get; set; }
    [JsonPropertyName("saveToSentItems")]
    public bool SaveToSentItems { get; set; }
}
