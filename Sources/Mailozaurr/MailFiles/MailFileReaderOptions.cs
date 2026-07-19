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
    /// <summary>Verifies retained EML, MSG, or TNEF S/MIME signatures through OfficeIMO.Security when true.</summary>
    public bool VerifySignature { get; set; }
}
