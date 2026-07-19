using MimeKit;
using OfficeIMO.Email;

namespace Mailozaurr;

/// <summary>Supported mail file formats.</summary>
public enum MailFileFormat {
    /// <summary>Outlook MSG format.</summary>
    Msg,
    /// <summary>RFC 822 EML format.</summary>
    Eml,
    /// <summary>Outlook OFT template format.</summary>
    OutlookTemplate,
    /// <summary>Transport Neutral Encapsulation Format, commonly winmail.dat.</summary>
    Tnef
}

/// <summary>Recipient classification.</summary>
public enum MailFileRecipientType {
    /// <summary>Unknown or unspecified recipient type.</summary>
    Unknown = 0,
    /// <summary>Primary recipient.</summary>
    To = 1,
    /// <summary>Carbon copy recipient.</summary>
    Cc = 2,
    /// <summary>Blind carbon copy recipient.</summary>
    Bcc = 3,
    /// <summary>Resource recipient.</summary>
    Resource = 4,
    /// <summary>Room recipient.</summary>
    Room = 5,
    /// <summary>Reply-to recipient.</summary>
    ReplyTo = 6
}

/// <summary>Represents an address from a mail file.</summary>
public sealed class MailFileAddress {
    /// <summary>Creates an address with optional display name and raw value.</summary>
    public MailFileAddress(string? address, string? displayName = null, string? raw = null,
        string? addressType = null) {
        Address = address;
        DisplayName = displayName;
        Raw = raw;
        AddressType = addressType;
    }

    /// <summary>Email or source-specific address value.</summary>
    public string? Address { get; }
    /// <summary>Display name value.</summary>
    public string? DisplayName { get; }
    /// <summary>Raw source value.</summary>
    public string? Raw { get; }
    /// <summary>Source address type such as SMTP or EX.</summary>
    public string? AddressType { get; }

    /// <inheritdoc />
    public override string ToString() {
        if (!string.IsNullOrWhiteSpace(DisplayName) && !string.IsNullOrWhiteSpace(Address)) {
            return $"{DisplayName} <{Address}>";
        }
        return !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName! : Address ?? Raw ?? string.Empty;
    }
}

/// <summary>Represents a recipient entry with type and address.</summary>
public sealed class MailFileRecipient {
    /// <summary>Creates a recipient entry.</summary>
    public MailFileRecipient(MailFileRecipientType type, MailFileAddress address) {
        Type = type;
        Address = address ?? throw new ArgumentNullException(nameof(address));
    }

    /// <summary>Recipient type.</summary>
    public MailFileRecipientType Type { get; }
    /// <summary>Recipient address.</summary>
    public MailFileAddress Address { get; }
}

/// <summary>Represents an attachment from a mail file.</summary>
public sealed class MailFileAttachment {
    /// <summary>Attachment file name.</summary>
    public string? FileName { get; set; }
    /// <summary>Attachment content type.</summary>
    public string? ContentType { get; set; }
    /// <summary>Attachment content id.</summary>
    public string? ContentId { get; set; }
    /// <summary>Attachment content location.</summary>
    public string? ContentLocation { get; set; }
    /// <summary>Indicates whether the attachment is inline.</summary>
    public bool IsInline { get; set; }
    /// <summary>Indicates whether Outlook hides the attachment.</summary>
    public bool IsHidden { get; set; }
    /// <summary>Indicates whether the attachment is a contact photo.</summary>
    public bool IsContactPhoto { get; set; }
    /// <summary>Attachment size in bytes.</summary>
    public long? Size { get; set; }
    /// <summary>Attachment content bytes.</summary>
    public byte[]? Content { get; set; }
    /// <summary>Embedded Outlook item when the attachment is structured.</summary>
    public EmailDocument? EmbeddedDocument { get; set; }
}

/// <summary>Represents a mail file backed by one mutable OfficeIMO document.</summary>
/// <remarks>Dispose the message when finished so OfficeIMO can release any temporary file-backed attachment content.</remarks>
public sealed partial class MailFileMessage : IDisposable {
    private readonly bool _includeAttachments;
    private readonly bool _includeAttachmentContent;
    private readonly bool _includeHeaders;

    internal MailFileMessage(string filePath, MailFileFormat format, EmailReadResult officeReadResult,
        MailFileSignatureInfo signature, MailFileReaderOptions options) {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        Format = format;
        OfficeReadResult = officeReadResult ?? throw new ArgumentNullException(nameof(officeReadResult));
        OfficeDocument = officeReadResult.Document;
        Diagnostics = officeReadResult.Diagnostics;
        SignatureVerification = signature.Verification;
        SignatureIsValid = signature.IsValid;
        SignedBy = signature.SignedBy;
        SignedOn = signature.SignedOn;
        _includeAttachments = options.IncludeAttachments;
        _includeAttachmentContent = options.IncludeAttachmentContent;
        _includeHeaders = options.IncludeHeaders;
    }

    /// <summary>Mail file format.</summary>
    public MailFileFormat Format { get; }
    /// <summary>Full file path.</summary>
    public string FilePath { get; }
    /// <summary>Message subject.</summary>
    public string? Subject => OfficeDocument.Subject;
    /// <summary>Sender address.</summary>
    public MailFileAddress? From => MailFileCompatibilityProjection.ProjectAddress(OfficeDocument.From);
    /// <summary>Actual sender when distinct from the represented author.</summary>
    public MailFileAddress? Sender => MailFileCompatibilityProjection.ProjectAddress(OfficeDocument.Sender);
    /// <summary>To recipients.</summary>
    public IReadOnlyList<MailFileAddress> To =>
        MailFileCompatibilityProjection.ProjectAddresses(OfficeDocument, MailFileRecipientType.To);
    /// <summary>Cc recipients.</summary>
    public IReadOnlyList<MailFileAddress> Cc =>
        MailFileCompatibilityProjection.ProjectAddresses(OfficeDocument, MailFileRecipientType.Cc);
    /// <summary>Bcc recipients.</summary>
    public IReadOnlyList<MailFileAddress> Bcc =>
        MailFileCompatibilityProjection.ProjectAddresses(OfficeDocument, MailFileRecipientType.Bcc);
    /// <summary>All recipients with types.</summary>
    public IReadOnlyList<MailFileRecipient> Recipients =>
        MailFileCompatibilityProjection.ProjectRecipients(OfficeDocument);
    /// <summary>Sent date.</summary>
    public DateTimeOffset? SentOn => OfficeDocument.Date;
    /// <summary>Received date.</summary>
    public DateTimeOffset? ReceivedOn => OfficeDocument.ReceivedDate;
    /// <summary>Creation date.</summary>
    public DateTimeOffset? CreatedOn => OfficeDocument.MessageMetadata.CreatedDate;
    /// <summary>Last modification date.</summary>
    public DateTimeOffset? ModifiedOn => OfficeDocument.MessageMetadata.ModifiedDate;
    /// <summary>Plain text body.</summary>
    public string? BodyText => OfficeDocument.Body.Text;
    /// <summary>HTML body.</summary>
    public string? BodyHtml => OfficeDocument.Body.Html;
    /// <summary>RTF body when present.</summary>
    public string? BodyRtf => OfficeDocument.Body.Rtf;
    /// <summary>Attachments list.</summary>
    public IReadOnlyList<MailFileAttachment> Attachments => MailFileCompatibilityProjection.ProjectAttachments(
        OfficeDocument, _includeAttachments, _includeAttachmentContent);
    /// <summary>Message id value.</summary>
    public string? MessageId => OfficeDocument.MessageId;
    /// <summary>Outlook message class.</summary>
    public string? MessageClass => OfficeDocument.MessageClass;
    /// <summary>Typed Outlook item classification.</summary>
    public OutlookItemKind OutlookItemKind => OfficeDocument.OutlookItemKind;
    /// <summary>Outlook categories.</summary>
    public IReadOnlyList<string> Categories => OfficeDocument.MessageMetadata.Categories.ToArray();
    /// <summary>Protected-message classification.</summary>
    public EmailProtectionKind ProtectionKind => OfficeDocument.Protection.Kind;
    /// <summary>Complete bounded OfficeIMO S/MIME verification result when verification was requested.</summary>
    public EmailSmimeVerificationResult? SignatureVerification { get; }
    /// <summary>Indicates whether the embedded S/MIME signature passed signature-only validation; null when absent or unverifiable.</summary>
    public bool? SignatureIsValid { get; }
    /// <summary>Signer identity projected from the embedded S/MIME certificate.</summary>
    public string? SignedBy { get; }
    /// <summary>Creation timestamp reported by the embedded S/MIME signature.</summary>
    public DateTimeOffset? SignedOn { get; }
    /// <summary>Raw headers merged into a single dictionary.</summary>
    public IReadOnlyDictionary<string, string>? Headers => _includeHeaders
        ? MailFileCompatibilityProjection.ProjectHeaders(OfficeDocument)
        : null;
    /// <summary>Structured OfficeIMO read diagnostics.</summary>
    public IReadOnlyList<EmailDiagnostic> Diagnostics { get; }
    /// <summary>True when the OfficeIMO reader produced at least one error diagnostic.</summary>
    public bool HasErrors {
        get {
            foreach (EmailDiagnostic diagnostic in Diagnostics) {
                if (diagnostic.Severity == EmailDiagnosticSeverity.Error) return true;
            }
            return false;
        }
    }
    /// <summary>Complete owner document, including typed Outlook items and retained MAPI values.</summary>
    public EmailDocument OfficeDocument { get; }
    /// <summary>Owner read result, including consumed bytes and file-backed attachment lifetime.</summary>
    public EmailReadResult OfficeReadResult { get; }

    /// <summary>Releases temporary file-backed attachment content owned by the OfficeIMO read result.</summary>
    public void Dispose() => OfficeReadResult.Dispose();

    /// <summary>Creates a MimeKit message from the current OfficeIMO owner document.</summary>
    public MimeMessage ToMimeMessage() => MailFileMimeAdapter.ToMimeMessage(OfficeDocument);

    /// <summary>Asynchronously creates a MimeKit message from the current OfficeIMO owner document.</summary>
    public Task<MimeMessage> ToMimeMessageAsync(CancellationToken cancellationToken = default) =>
        MailFileMimeAdapter.ToMimeMessageAsync(OfficeDocument, cancellationToken);

    /// <summary>Attempts to expose the protected MSG payload as a MimeKit entity.</summary>
    public bool TryGetProtectedMimeEntity(out MimeEntity? entity) =>
        MailFileMimeAdapter.TryGetProtectedMimeEntity(OfficeDocument, out entity);
}
