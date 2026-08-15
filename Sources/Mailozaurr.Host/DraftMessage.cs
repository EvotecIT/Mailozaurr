namespace Mailozaurr.Hosting;

/// <summary>
/// Represents a normalized draft message before it is queued or sent.
/// </summary>
public sealed class DraftMessage {
    /// <summary>Source profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Sender address or display entry.</summary>
    public MessageRecipient? From { get; set; }

    /// <summary>Primary recipients.</summary>
    public List<MessageRecipient> To { get; set; } = new();

    /// <summary>Carbon-copy recipients.</summary>
    public List<MessageRecipient> Cc { get; set; } = new();

    /// <summary>Blind carbon-copy recipients.</summary>
    public List<MessageRecipient> Bcc { get; set; } = new();

    /// <summary>Reply-to recipients.</summary>
    public List<MessageRecipient> ReplyTo { get; set; } = new();

    /// <summary>Message subject.</summary>
    public string? Subject { get; set; }

    /// <summary>Plain text body.</summary>
    public string? TextBody { get; set; }

    /// <summary>HTML body.</summary>
    public string? HtmlBody { get; set; }

    /// <summary>Message priority shared by all delivery providers.</summary>
    public MessagePriority Priority { get; set; } = MessagePriority.Normal;

    /// <summary>Attachments included with the draft.</summary>
    public List<DraftAttachment> Attachments { get; set; } = new();

    /// <summary>Optional custom headers.</summary>
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
