namespace Mailozaurr;

/// <summary>
/// Represents a message to be sent using SendGrid.
/// </summary>
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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SendGridAttachment>? Attachments { get; set; }
}

/// <summary>
/// Represents an email address in a SendGrid message.
/// </summary>
public class SendGridEmailAddress {
    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the name associated with the email address.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }
}

/// <summary>
/// Represents a personalization in a SendGrid message.
/// </summary>
public class SendGridPersonalization {
    /// <summary>
    /// Gets or sets the list of recipients for the message.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SendGridEmailAddress>? To { get; set; }

    /// <summary>
    /// Gets or sets the list of CC recipients for the message.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SendGridEmailAddress>? Cc { get; set; }

    /// <summary>
    /// Gets or sets the list of BCC recipients for the message.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SendGridEmailAddress>? Bcc { get; set; }
}

/// <summary>
/// Represents the content of a SendGrid message.
/// </summary>
public class SendGridContent {
    /// <summary>
    /// Gets or sets the type of the content.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the value of the content.
    /// </summary>
    public string? Value { get; set; }
}

/// <summary>
/// Represents an attachment in a SendGrid message.
/// </summary>
public class SendGridAttachment {
    /// <summary>
    /// Gets or sets the filename of the attachment.
    /// </summary>
    public string Filename { get; set; }

    /// <summary>
    /// Gets or sets the content of the attachment, encoded in Base64.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the MIME type of the attachment.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the disposition of the attachment.
    /// </summary>
    public string Disposition { get; set; }

    /// <summary>
    /// Initializes a new instance of the SendGridAttachment class with the specified file path.
    /// The file is read from the file system, and its content is converted to Base64.
    /// </summary>
    /// <param name="filePath">The path of the file to attach.</param>
    public SendGridAttachment(string filePath) {
        Filename = Path.GetFileName(filePath);
        var bytes = File.ReadAllBytes(filePath);
        Content = Convert.ToBase64String(bytes);
        Type = "application/octet-stream"; // Update this if you have the MIME type
        Disposition = "attachment";
    }
}
