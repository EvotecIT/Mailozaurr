namespace Mailozaurr;

/// <summary>
/// Represents an email message for use with the Graph API.
/// </summary>
public class GraphMessage {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("from")]
    public GraphEmailAddress From { get; set; }

    [JsonPropertyName("toRecipients")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphEmailAddress>? To { get; set; }

    [JsonPropertyName("ccRecipients")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphEmailAddress>? Cc { get; set; }

    [JsonPropertyName("bccRecipients")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphEmailAddress>? Bcc { get; set; }

    [JsonPropertyName("replyTo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphEmailAddress>? ReplyTo { get; set; }

    [JsonPropertyName("subject")]
    public string Subject { get; set; }

    [JsonPropertyName("body")]
    public GraphContent Body { get; set; }

    [JsonPropertyName("importance")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Importance { get; set; }

    [JsonPropertyName("isReadReceiptRequested")]
    public bool IsReadReceiptRequested { get; set; }

    [JsonPropertyName("isDeliveryReceiptRequested")]
    public bool IsDeliveryReceiptRequested { get; set; }

    [JsonPropertyName("attachments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphAttachment>? Attachments { get; set; }
}