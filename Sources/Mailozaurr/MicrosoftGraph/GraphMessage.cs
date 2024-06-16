namespace Mailozaurr;

public class GraphMessageContainer {
    [JsonPropertyName("message")]
    public GraphMessage Message { get; set; }
    [JsonPropertyName("saveToSentItems")]
    public bool SaveToSentItems { get; set; }
}

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
    public List<GraphEmailAddress>? ReplyTo { get; set; }

    [JsonPropertyName("subject")]
    public string Subject { get; set; }

    [JsonPropertyName("body")]
    public GraphContent Body { get; set; }

    [JsonPropertyName("importance")]
    public string Importance { get; set; }

    [JsonPropertyName("isReadReceiptRequested")]
    public bool IsReadReceiptRequested { get; set; }

    [JsonPropertyName("isDeliveryReceiptRequested")]
    public bool IsDeliveryReceiptRequested { get; set; }

    [JsonPropertyName("attachments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphAttachment>? Attachments { get; set; }
}

public class GraphEmailAddress {
    [JsonPropertyName("emailAddress")]
    public GraphEmail Email { get; set; }
}

public class GraphEmail {
    [JsonPropertyName("address")]
    public string Address { get; set; }
}

public class GraphContent {
    [JsonPropertyName("contentType")]
    public string Type { get; set; } = "Text";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}