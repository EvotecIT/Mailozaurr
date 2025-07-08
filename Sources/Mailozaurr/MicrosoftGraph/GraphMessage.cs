namespace Mailozaurr;

/// <summary>
/// Represents an email message for use with the Graph API.
/// </summary>
public class GraphMessage {
    /// <summary>
    /// Gets or sets the message identifier.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("id")]
    public string Id { get; set; }
    /// <summary>
    /// Gets or sets the sender address.
    /// </summary>
    [JsonPropertyName("from")]
    public GraphEmailAddress From { get; set; }

    /// <summary>
    /// Gets or sets the primary recipients of the message.
    /// </summary>
    [JsonPropertyName("toRecipients")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphEmailAddress>? To { get; set; }

    /// <summary>
    /// Gets or sets the CC recipients of the message.
    /// </summary>
    [JsonPropertyName("ccRecipients")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphEmailAddress>? Cc { get; set; }

    /// <summary>
    /// Gets or sets the BCC recipients of the message.
    /// </summary>
    [JsonPropertyName("bccRecipients")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphEmailAddress>? Bcc { get; set; }

    /// <summary>
    /// Gets or sets reply-to addresses for the message.
    /// </summary>
    [JsonPropertyName("replyTo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphEmailAddress>? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the subject line of the message.
    /// </summary>
    [JsonPropertyName("subject")]
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the message body.
    /// </summary>
    [JsonPropertyName("body")]
    public GraphContent Body { get; set; }

    /// <summary>
    /// Gets or sets the importance of the message.
    /// </summary>
    [JsonPropertyName("importance")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Importance { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a read receipt is requested.
    /// </summary>
    [JsonPropertyName("isReadReceiptRequested")]
    public bool IsReadReceiptRequested { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a delivery receipt is requested.
    /// </summary>
    [JsonPropertyName("isDeliveryReceiptRequested")]
    public bool IsDeliveryReceiptRequested { get; set; }

    /// <summary>
    /// Gets or sets attachments included with the message.
    /// </summary>
    [JsonPropertyName("attachments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphAttachment>? Attachments { get; set; }

    [JsonPropertyName("internetMessageHeaders")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphInternetMessageHeader>? InternetMessageHeaders { get; set; }}