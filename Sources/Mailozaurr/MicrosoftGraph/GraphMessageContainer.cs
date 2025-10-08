namespace Mailozaurr;

/// <summary>
/// Wrapper used when serializing a message for Graph API calls.
/// </summary>
/// <remarks>
/// Allows additional properties like <c>saveToSentItems</c> to be
/// specified alongside the message body.
/// </remarks>
public class GraphMessageContainer {
    /// <summary>
    /// Gets or sets the message payload.
    /// </summary>
    [JsonPropertyName("message")]
    public GraphMessage Message { get; set; } = new GraphMessage();

    /// <summary>
    /// Gets or sets a value indicating whether the message should be saved to the Sent Items folder.
    /// </summary>
    [JsonPropertyName("saveToSentItems")]
    public bool SaveToSentItems { get; set; }
}
