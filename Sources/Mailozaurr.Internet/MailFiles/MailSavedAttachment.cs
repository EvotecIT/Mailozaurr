using OfficeIMO.Email;

namespace Mailozaurr;

/// <summary>A committed attachment with original metadata and source provenance.</summary>
public sealed class MailSavedAttachment {
    internal MailSavedAttachment(EmailMessageAttachment attachment, EmailAttachmentExtractionEntry entry) {
        Attachment = attachment; Path = entry.OutputPath!; BytesWritten = entry.BytesWritten; Sha256 = entry.Sha256;
    }
    /// <summary>Source attachment metadata.</summary>
    public EmailMessageAttachment Attachment { get; }
    /// <summary>Absolute saved path.</summary>
    public string Path { get; }
    /// <summary>Original filename, before sanitization.</summary>
    public string? OriginalFileName => Attachment.FileName;
    /// <summary>Declared media type.</summary>
    public string? ContentType => Attachment.ContentType;
    /// <summary>Inline disposition.</summary>
    public bool IsInline => Attachment.IsInline;
    /// <summary>Embedded resource identifier.</summary>
    public string? ContentId => Attachment.ContentId;
    /// <summary>Source file or archive.</summary>
    public string SourcePath => Attachment.Message.SourcePath;
    /// <summary>Source folder.</summary>
    public string? FolderPath => Attachment.Message.FolderPath;
    /// <summary>Source message identifier.</summary>
    public string MessageId => Attachment.Message.Id;
    /// <summary>Committed byte count.</summary>
    public long BytesWritten { get; }
    /// <summary>SHA-256 of the committed file.</summary>
    public string? Sha256 { get; }
}
