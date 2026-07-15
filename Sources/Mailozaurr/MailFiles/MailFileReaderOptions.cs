using MimeKit;
using OfficeIMO.Email;

namespace Mailozaurr;

/// <summary>Options for reading mail files.</summary>
public sealed class MailFileReaderOptions {
    /// <summary>Includes attachment metadata when true.</summary>
    public bool IncludeAttachments { get; set; } = true;
    /// <summary>Includes attachment content bytes when true.</summary>
    public bool IncludeAttachmentContent { get; set; } = true;
    /// <summary>Includes raw headers in the compatibility projection when true.</summary>
    public bool IncludeHeaders { get; set; }
    /// <summary>Optional immutable OfficeIMO bounded-reader policy.</summary>
    public EmailReaderOptions? OfficeReaderOptions { get; set; }
    /// <summary>Verifies S/MIME signatures through MimeKit when true. This performs an additional EML parse.</summary>
    public bool VerifySignature { get; set; }
    /// <summary>Optional MimeKit parser policy used when S/MIME signature verification is enabled.</summary>
    public ParserOptions? MimeParserOptions { get; set; }
}
