namespace Mailozaurr;

/// <summary>
/// Represents the content of a SendGrid message.
/// </summary>
public class SendGridContent {
    /// <summary>Gets or sets the type of the content.</summary>
    public string? Type { get; set; }

    /// <summary>Gets or sets the value of the content.</summary>
    public string? Value { get; set; }
}
