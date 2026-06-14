namespace Mailozaurr;

using MimeKit;

/// <summary>
/// Represents text and HTML bodies extracted from a MIME message.
/// </summary>
/// <remarks>
/// Useful when converting between different message formats or when
/// sanitizing HTML before sending.
/// </remarks>
public class MimeMessageContent {
    /// <summary>Creates a new instance from the specified message.</summary>
    /// <param name="message">Source MIME message.</param>
    public MimeMessageContent(MimeMessage message) {
        TextBody = message.TextBody;
        HtmlBody = message.HtmlBody;
    }

    /// <summary>Plain text body.</summary>
    public string? TextBody { get; }

    /// <summary>HTML body.</summary>
    public string? HtmlBody { get; }
}