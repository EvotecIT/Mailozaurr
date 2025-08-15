namespace Mailozaurr;

/// <summary>
/// Represents an email address in a SendGrid message.
/// </summary>
/// <remarks>
/// SendGrid expects addresses in this structured form when
/// constructing the JSON payload.
/// </remarks>
public class SendGridEmailAddress {
    /// <summary>Gets or sets the email address.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets the name associated with the email address.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }
}
