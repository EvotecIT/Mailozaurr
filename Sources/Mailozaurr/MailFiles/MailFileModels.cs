using MimeKit;
using OfficeIMO.Email;

namespace Mailozaurr;

/// <summary>Supported mail file formats.</summary>
public enum MailFileFormat {
    /// <summary>Outlook MSG format.</summary>
    Msg,
    /// <summary>RFC 822 EML format.</summary>
    Eml
}

/// <summary>Recipient classification.</summary>
public enum MailFileRecipientType {
    /// <summary>Unknown or unspecified recipient type.</summary>
    Unknown,
    /// <summary>Primary recipient.</summary>
    To,
    /// <summary>Carbon copy recipient.</summary>
    Cc,
    /// <summary>Blind carbon copy recipient.</summary>
    Bcc,
    /// <summary>Reply-to recipient.</summary>
    ReplyTo,
    /// <summary>Resource recipient.</summary>
    Resource,
    /// <summary>Room recipient.</summary>
    Room
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

/// <summary>Represents a mail file with compatibility fields and rich owner models.</summary>
public sealed class MailFileMessage {
    /// <summary>Mail file format.</summary>
    public MailFileFormat Format { get; set; }
    /// <summary>Full file path.</summary>
    public string FilePath { get; set; } = string.Empty;
    /// <summary>Message subject.</summary>
    public string? Subject { get; set; }
    /// <summary>Sender address.</summary>
    public MailFileAddress? From { get; set; }
    /// <summary>Actual sender when distinct from the represented author.</summary>
    public MailFileAddress? Sender { get; set; }
    /// <summary>To recipients.</summary>
    public IReadOnlyList<MailFileAddress> To { get; set; } = Array.Empty<MailFileAddress>();
    /// <summary>Cc recipients.</summary>
    public IReadOnlyList<MailFileAddress> Cc { get; set; } = Array.Empty<MailFileAddress>();
    /// <summary>Bcc recipients.</summary>
    public IReadOnlyList<MailFileAddress> Bcc { get; set; } = Array.Empty<MailFileAddress>();
    /// <summary>All recipients with types.</summary>
    public IReadOnlyList<MailFileRecipient> Recipients { get; set; } = Array.Empty<MailFileRecipient>();
    /// <summary>Sent date.</summary>
    public DateTimeOffset? SentOn { get; set; }
    /// <summary>Received date.</summary>
    public DateTimeOffset? ReceivedOn { get; set; }
    /// <summary>Creation date.</summary>
    public DateTimeOffset? CreatedOn { get; set; }
    /// <summary>Last modification date.</summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>Plain text body.</summary>
    public string? BodyText { get; set; }
    /// <summary>HTML body.</summary>
    public string? BodyHtml { get; set; }
    /// <summary>RTF body when present.</summary>
    public string? BodyRtf { get; set; }
    /// <summary>Attachments list.</summary>
    public IReadOnlyList<MailFileAttachment> Attachments { get; set; } = Array.Empty<MailFileAttachment>();
    /// <summary>Message id value.</summary>
    public string? MessageId { get; set; }
    /// <summary>Outlook message class.</summary>
    public string? MessageClass { get; set; }
    /// <summary>Typed Outlook item classification.</summary>
    public OutlookItemKind OutlookItemKind { get; set; }
    /// <summary>Outlook categories.</summary>
    public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();
    /// <summary>Protected-message classification.</summary>
    public EmailProtectionKind ProtectionKind { get; set; }
    /// <summary>Indicates whether a signature is valid after explicit host-side verification.</summary>
    public bool? SignatureIsValid { get; set; }
    /// <summary>Signer identity, if supplied by a host-side verifier.</summary>
    public string? SignedBy { get; set; }
    /// <summary>Signature timestamp, if supplied by a host-side verifier.</summary>
    public DateTimeOffset? SignedOn { get; set; }
    /// <summary>Raw headers merged into a single dictionary.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; set; }
    /// <summary>Structured OfficeIMO read diagnostics.</summary>
    public IReadOnlyList<EmailDiagnostic> Diagnostics { get; set; } = Array.Empty<EmailDiagnostic>();
    /// <summary>Complete owner document, including typed Outlook items and retained MAPI values.</summary>
    public EmailDocument OfficeDocument { get; set; } = new EmailDocument();
    /// <summary>Native MimeKit message for EML input. MSG callers can use <see cref="ToMimeMessage"/>.</summary>
    public MimeMessage? MimeMessage { get; set; }

    /// <summary>Returns the native MimeKit message, generating it from the OfficeIMO owner document when needed.</summary>
    public MimeMessage ToMimeMessage() => MimeMessage ?? MailFileMimeAdapter.ToMimeMessage(OfficeDocument);

    /// <summary>Attempts to expose the protected MSG payload as a MimeKit entity.</summary>
    public bool TryGetProtectedMimeEntity(out MimeEntity? entity) =>
        MailFileMimeAdapter.TryGetProtectedMimeEntity(OfficeDocument, out entity);
}
