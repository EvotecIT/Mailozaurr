namespace Mailozaurr;

/// <summary>
/// Represents a personalization in a SendGrid message.
/// </summary>
/// <remarks>
/// SendGrid supports multiple personalizations per message to send
/// customized content to several recipients.
/// </remarks>
public class SendGridPersonalization {
    /// <summary>Gets or sets the list of recipients for the message.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SendGridEmailAddress>? To { get; set; }

    /// <summary>Gets or sets the list of CC recipients for the message.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SendGridEmailAddress>? Cc { get; set; }

    /// <summary>Gets or sets the list of BCC recipients for the message.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SendGridEmailAddress>? Bcc { get; set; }
}