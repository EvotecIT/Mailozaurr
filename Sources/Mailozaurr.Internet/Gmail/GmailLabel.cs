using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>Gmail label returned by the Gmail API.</summary>
public sealed class GmailLabel {
    /// <summary>Label id (for system labels often equals the name).</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Label name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Label type (for example "system" or "user").</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Visibility of messages with this label in message lists.</summary>
    [JsonPropertyName("messageListVisibility")]
    public string? MessageListVisibility { get; set; }

    /// <summary>Visibility of the label in label lists.</summary>
    [JsonPropertyName("labelListVisibility")]
    public string? LabelListVisibility { get; set; }

    /// <summary>Total messages carrying the label.</summary>
    [JsonPropertyName("messagesTotal")]
    public long? MessagesTotal { get; set; }

    /// <summary>Unread messages carrying the label.</summary>
    [JsonPropertyName("messagesUnread")]
    public long? MessagesUnread { get; set; }

    /// <summary>Total threads carrying the label.</summary>
    [JsonPropertyName("threadsTotal")]
    public long? ThreadsTotal { get; set; }

    /// <summary>Unread threads carrying the label.</summary>
    [JsonPropertyName("threadsUnread")]
    public long? ThreadsUnread { get; set; }

    /// <summary>Optional provider-defined label colors.</summary>
    [JsonPropertyName("color")]
    public GmailLabelColor? Color { get; set; }
}

/// <summary>Gmail label text and background colors.</summary>
public sealed class GmailLabelColor {
    /// <summary>Text color.</summary>
    [JsonPropertyName("textColor")]
    public string? TextColor { get; set; }

    /// <summary>Background color.</summary>
    [JsonPropertyName("backgroundColor")]
    public string? BackgroundColor { get; set; }
}
