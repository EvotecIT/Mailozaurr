namespace Mailozaurr;

/// <summary>Options for reading mail files.</summary>
public sealed class MailFileReaderOptions {
    /// <summary>Includes attachment metadata when true.</summary>
    public bool IncludeAttachments { get; set; } = true;
    /// <summary>Includes attachment content bytes when true.</summary>
    public bool IncludeAttachmentContent { get; set; } = true;
    /// <summary>Includes raw headers when true.</summary>
    public bool IncludeHeaders { get; set; }
}
