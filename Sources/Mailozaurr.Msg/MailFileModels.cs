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
    /// <summary>Resource recipient.</summary>
    Resource,
    /// <summary>Room recipient.</summary>
    Room
}

/// <summary>Represents an address from a mail file.</summary>
public sealed class MailFileAddress {
    /// <summary>Creates an address with optional display name and raw value.</summary>
    /// <param name="address">Email address value.</param>
    /// <param name="displayName">Display name value.</param>
    /// <param name="raw">Raw header value.</param>
    public MailFileAddress(string? address, string? displayName = null, string? raw = null) {
        Address = address;
        DisplayName = displayName;
        Raw = raw;
    }

    /// <summary>Email address value.</summary>
    public string? Address { get; }
    /// <summary>Display name value.</summary>
    public string? DisplayName { get; }
    /// <summary>Raw header value.</summary>
    public string? Raw { get; }

    /// <summary>Returns a formatted display string.</summary>
    public override string ToString() {
        if (!string.IsNullOrWhiteSpace(DisplayName) && !string.IsNullOrWhiteSpace(Address)) {
            return $"{DisplayName} <{Address}>";
        }
        if (!string.IsNullOrWhiteSpace(DisplayName)) {
            return DisplayName!;
        }
        return Address ?? string.Empty;
    }
}

/// <summary>Represents a recipient entry with type and address.</summary>
public sealed class MailFileRecipient {
    /// <summary>Creates a recipient entry.</summary>
    /// <param name="type">Recipient type.</param>
    /// <param name="address">Recipient address.</param>
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
    /// <summary>Indicates whether the attachment is inline.</summary>
    public bool IsInline { get; set; }
    /// <summary>Attachment size in bytes.</summary>
    public long? Size { get; set; }
    /// <summary>Attachment content bytes.</summary>
    public byte[]? Content { get; set; }
}

/// <summary>Represents a mail file with metadata and content.</summary>
public sealed class MailFileMessage {
    /// <summary>Mail file format.</summary>
    public MailFileFormat Format { get; set; }
    /// <summary>Full file path.</summary>
    public string FilePath { get; set; } = string.Empty;
    /// <summary>Message subject.</summary>
    public string? Subject { get; set; }
    /// <summary>Sender address.</summary>
    public MailFileAddress? From { get; set; }
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
    /// <summary>Plain text body.</summary>
    public string? BodyText { get; set; }
    /// <summary>HTML body.</summary>
    public string? BodyHtml { get; set; }
    /// <summary>Attachments list.</summary>
    public IReadOnlyList<MailFileAttachment> Attachments { get; set; } = Array.Empty<MailFileAttachment>();
    /// <summary>Message id value.</summary>
    public string? MessageId { get; set; }
    /// <summary>Indicates whether a signature is valid.</summary>
    public bool? SignatureIsValid { get; set; }
    /// <summary>Signer identity, if available.</summary>
    public string? SignedBy { get; set; }
    /// <summary>Signature timestamp.</summary>
    public DateTimeOffset? SignedOn { get; set; }
    /// <summary>Raw headers merged into a single dictionary.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; set; }
}
