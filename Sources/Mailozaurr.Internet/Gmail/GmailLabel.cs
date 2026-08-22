namespace Mailozaurr;

/// <summary>Gmail label returned by the Gmail API.</summary>
public sealed class GmailLabel {
    /// <summary>Label id (for system labels often equals the name).</summary>
    public string? Id { get; set; }

    /// <summary>Label name.</summary>
    public string? Name { get; set; }

    /// <summary>Label type (for example "system" or "user").</summary>
    public string? Type { get; set; }

    /// <summary>Visibility of messages with this label in message lists.</summary>
    public string? MessageListVisibility { get; set; }

    /// <summary>Visibility of the label in label lists.</summary>
    public string? LabelListVisibility { get; set; }

    /// <summary>Total messages carrying the label.</summary>
    public long? MessagesTotal { get; set; }

    /// <summary>Unread messages carrying the label.</summary>
    public long? MessagesUnread { get; set; }

    /// <summary>Total threads carrying the label.</summary>
    public long? ThreadsTotal { get; set; }

    /// <summary>Unread threads carrying the label.</summary>
    public long? ThreadsUnread { get; set; }

    /// <summary>Optional provider-defined label colors.</summary>
    public GmailLabelColor? Color { get; set; }
}

/// <summary>Gmail label text and background colors.</summary>
public sealed class GmailLabelColor {
    /// <summary>Text color.</summary>
    public string? TextColor { get; set; }

    /// <summary>Background color.</summary>
    public string? BackgroundColor { get; set; }
}
