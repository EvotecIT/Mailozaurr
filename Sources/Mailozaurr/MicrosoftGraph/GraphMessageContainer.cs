namespace Mailozaurr;

/// <summary>
/// Wrapper used when serializing a message for Graph API calls.
/// </summary>
public class GraphMessageContainer {
    /// <summary>
    /// Gets or sets the message payload.
    /// </summary>
    [JsonPropertyName("message")]
    public GraphMessage Message { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the message should be saved to the Sent Items folder.
    /// </summary>
    [JsonPropertyName("saveToSentItems")]
    public bool SaveToSentItems { get; set; }
}
