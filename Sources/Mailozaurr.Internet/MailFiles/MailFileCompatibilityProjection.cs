using OfficeIMO.Email;

namespace Mailozaurr;

/// <summary>Projects the mutable OfficeIMO owner into the legacy Mailozaurr read model.</summary>
internal static class MailFileCompatibilityProjection {
    internal static MailFileAddress? ProjectAddress(EmailAddress? address) => address == null
        ? null
        : new MailFileAddress(address.Address, address.DisplayName, address.RawValue, address.AddressType);

    internal static IReadOnlyList<MailFileRecipient> ProjectRecipients(EmailDocument document) =>
        document.Recipients.Select(ProjectRecipient).ToArray();

    internal static IReadOnlyList<MailFileAddress> ProjectAddresses(EmailDocument document,
        MailFileRecipientType type) => document.Recipients
        .Where(recipient => MapRecipientType(recipient.Kind) == type)
        .Select(recipient => ProjectAddress(recipient.Address)!)
        .ToArray();

    internal static IReadOnlyList<MailFileAttachment> ProjectAttachments(EmailDocument document,
        bool includeAttachments, bool includeContent) => includeAttachments
        ? document.Attachments.Select(attachment => ProjectAttachment(attachment, includeContent)).ToArray()
        : Array.Empty<MailFileAttachment>();

    internal static IReadOnlyDictionary<string, string> ProjectHeaders(EmailDocument document) {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (EmailHeader header in document.Headers) {
            if (result.TryGetValue(header.Name, out string? existing)) {
                result[header.Name] = string.Concat(existing, ", ", header.Value);
            } else {
                result[header.Name] = header.Value;
            }
        }
        return result;
    }

    private static MailFileAttachment ProjectAttachment(EmailAttachment attachment, bool includeContent) => new() {
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        ContentId = attachment.ContentId,
        ContentLocation = attachment.ContentLocation,
        IsInline = attachment.IsInline,
        IsHidden = attachment.IsHidden,
        IsContactPhoto = attachment.IsContactPhoto,
        Size = attachment.Length,
        Content = includeContent ? attachment.Content : null,
        EmbeddedDocument = attachment.EmbeddedDocument
    };

    private static MailFileRecipient ProjectRecipient(EmailRecipient recipient) =>
        new(MapRecipientType(recipient.Kind), ProjectAddress(recipient.Address)!);

    private static MailFileRecipientType MapRecipientType(EmailRecipientKind kind) => kind switch {
        EmailRecipientKind.To => MailFileRecipientType.To,
        EmailRecipientKind.Cc => MailFileRecipientType.Cc,
        EmailRecipientKind.Bcc => MailFileRecipientType.Bcc,
        EmailRecipientKind.ReplyTo => MailFileRecipientType.ReplyTo,
        EmailRecipientKind.Resource => MailFileRecipientType.Resource,
        EmailRecipientKind.Room => MailFileRecipientType.Room,
        _ => MailFileRecipientType.Unknown
    };
}
