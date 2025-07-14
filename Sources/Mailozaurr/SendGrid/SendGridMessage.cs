namespace Mailozaurr;

/// <summary>
/// Represents a message to be sent using SendGrid.
/// </summary>
/// <remarks>
/// This is a simplified model tailored for the module and does not
/// expose every SendGrid feature.
/// </remarks>
public class SendGridMessage {
    /// <summary>
    /// Gets or sets the list of personalizations for the message.
    /// </summary>
    public List<SendGridPersonalization> Personalizations { get; set; }

    /// <summary>
    /// Gets or sets the sender of the message.
    /// </summary>
    public SendGridEmailAddress From { get; set; }

    /// <summary>
    /// Gets or sets the subject of the message.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the content of the message.
    /// </summary>
    public List<SendGridContent> Content { get; set; }

    /// <summary>
    /// Gets or sets the reply-to address for the message.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SendGridEmailAddress? ReplyTo { get; set; }

    /// <summary>Attachments to include with the message.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SendGridAttachment>? Attachments { get; set; }

    /// <summary>Custom headers to include with the message.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Headers { get; set; }
}
