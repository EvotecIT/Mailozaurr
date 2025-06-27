namespace Mailozaurr;

/// <summary>
/// Represents an email address in a SendGrid message.
/// </summary>
public class SendGridEmailAddress {
    /// <summary>Gets or sets the email address.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Email { get; set; }

    /// <summary>Gets or sets the name associated with the email address.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }
}
