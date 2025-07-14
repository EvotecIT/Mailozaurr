namespace Mailozaurr;

using MimeKit;

/// <summary>
/// Represents text and HTML bodies extracted from a MIME message.
/// </summary>
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
