namespace Mailozaurr;

/// <summary>
/// Describes a deliberate SMTP validation message sent during mail-flow diagnostics.
/// </summary>
public sealed class SmtpValidationMessageRequest {
    /// <summary>Envelope and header sender used for the validation message.</summary>
    public string Sender { get; set; } = "probe@example.com";
    /// <summary>Recipient that receives the validation message.</summary>
    public string Recipient { get; set; } = string.Empty;
    /// <summary>EHLO/HELO name used by the SMTP client.</summary>
    public string HeloHost { get; set; } = "localhost";
    /// <summary>Optional stable test identifier. A value is generated when omitted.</summary>
    public string? TestId { get; set; }
    /// <summary>Optional validation subject. A neutral subject is generated when omitted.</summary>
    public string? Subject { get; set; }
    /// <summary>Optional validation body. A neutral body is generated when omitted.</summary>
    public string? Body { get; set; }
    /// <summary>Marks the validation message as high priority.</summary>
    public bool HighPriority { get; set; }
}
