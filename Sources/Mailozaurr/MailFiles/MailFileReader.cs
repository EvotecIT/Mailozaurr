using MimeKit;
using OfficeIMO.Email;

namespace Mailozaurr;

/// <summary>Reads MSG and EML files into compatibility fields and their rich owner models.</summary>
public static class MailFileReader {
    /// <summary>Reads a mail file from a path.</summary>
    public static MailFileMessage Read(string path, MailFileReaderOptions? options = null) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));
        }
        return Read(new FileInfo(path), options);
    }

    /// <summary>Reads a mail file from a file descriptor.</summary>
    public static MailFileMessage Read(FileInfo fileInfo, MailFileReaderOptions? options = null) {
        ValidateFile(fileInfo);
        options ??= new MailFileReaderOptions();
        MailFileFormat format = ResolveFormat(fileInfo);
        EmailReaderOptions officeOptions = ResolveOfficeOptions(options);
        EmailReadResult result = new EmailDocumentReader(officeOptions).Read(fileInfo.FullName);
        EnsureExpectedFormat(result.Document, format, fileInfo.FullName);

        MimeMessage? mimeMessage = format == MailFileFormat.Eml
            ? LoadMimeMessage(fileInfo.FullName, options.MimeParserOptions)
            : null;
        return Project(fileInfo.FullName, format, result, mimeMessage, options);
    }

    /// <summary>Asynchronously reads a mail file from a path.</summary>
    public static Task<MailFileMessage> ReadAsync(string path, MailFileReaderOptions? options = null,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));
        }
        return ReadAsync(new FileInfo(path), options, cancellationToken);
    }

    /// <summary>Asynchronously reads a mail file from a file descriptor.</summary>
    public static async Task<MailFileMessage> ReadAsync(FileInfo fileInfo, MailFileReaderOptions? options = null,
        CancellationToken cancellationToken = default) {
        ValidateFile(fileInfo);
        options ??= new MailFileReaderOptions();
        MailFileFormat format = ResolveFormat(fileInfo);
        EmailReaderOptions officeOptions = ResolveOfficeOptions(options);
        EmailReadResult result = await new EmailDocumentReader(officeOptions)
            .ReadAsync(fileInfo.FullName, cancellationToken).ConfigureAwait(false);
        EnsureExpectedFormat(result.Document, format, fileInfo.FullName);

        MimeMessage? mimeMessage = format == MailFileFormat.Eml
            ? await LoadMimeMessageAsync(fileInfo.FullName, options.MimeParserOptions, cancellationToken)
                .ConfigureAwait(false)
            : null;
        return Project(fileInfo.FullName, format, result, mimeMessage, options);
    }

    /// <summary>Attempts to read a mail file and returns an error message on failure.</summary>
    public static bool TryRead(string path, out MailFileMessage? message, out string? error,
        MailFileReaderOptions? options = null) {
        message = null;
        error = null;
        if (string.IsNullOrWhiteSpace(path)) {
            error = "File path is empty.";
            return false;
        }
        if (!File.Exists(path)) {
            error = $"File {path} doesn't exist.";
            return false;
        }
        try {
            message = Read(path, options);
            return true;
        } catch (NotSupportedException) {
            error = $"File {path} is not a .msg or .eml file.";
            return false;
        } catch (Exception ex) {
            error = $"File {path} is not a .msg or .eml file or another error occurred. Error: {ex.Message}";
            return false;
        }
    }

    private static MailFileMessage Project(string path, MailFileFormat format, EmailReadResult result,
        MimeMessage? mimeMessage, MailFileReaderOptions options) {
        EmailDocument document = result.Document;
        List<MailFileRecipient> recipients = document.Recipients.Select(ProjectRecipient).ToList();
        IReadOnlyList<MailFileAttachment> attachments = options.IncludeAttachments
            ? document.Attachments.Select(attachment =>
                ProjectAttachment(attachment, options.IncludeAttachmentContent)).ToArray()
            : Array.Empty<MailFileAttachment>();

        return new MailFileMessage {
            Format = format,
            FilePath = path,
            Subject = document.Subject,
            From = ProjectAddress(document.From),
            Sender = ProjectAddress(document.Sender),
            To = SelectAddresses(recipients, MailFileRecipientType.To),
            Cc = SelectAddresses(recipients, MailFileRecipientType.Cc),
            Bcc = SelectAddresses(recipients, MailFileRecipientType.Bcc),
            Recipients = recipients,
            SentOn = document.Date,
            ReceivedOn = document.ReceivedDate,
            CreatedOn = document.MessageMetadata.CreatedDate,
            ModifiedOn = document.MessageMetadata.ModifiedDate,
            BodyText = document.Body.Text,
            BodyHtml = document.Body.Html,
            BodyRtf = document.Body.Rtf,
            Attachments = attachments,
            MessageId = document.MessageId,
            MessageClass = document.MessageClass,
            OutlookItemKind = document.OutlookItemKind,
            Categories = document.MessageMetadata.Categories.ToArray(),
            ProtectionKind = document.Protection.Kind,
            Headers = options.IncludeHeaders ? MergeHeaders(document.Headers) : null,
            Diagnostics = result.Diagnostics,
            OfficeDocument = document,
            MimeMessage = mimeMessage
        };
    }

    private static MailFileAttachment ProjectAttachment(EmailAttachment attachment, bool includeContent) => new MailFileAttachment {
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
        new MailFileRecipient(MapRecipientType(recipient.Kind), ProjectAddress(recipient.Address)!);

    private static MailFileAddress? ProjectAddress(EmailAddress? address) => address == null
        ? null
        : new MailFileAddress(address.Address, address.DisplayName, address.RawValue, address.AddressType);

    private static IReadOnlyList<MailFileAddress> SelectAddresses(IEnumerable<MailFileRecipient> recipients,
        MailFileRecipientType type) => recipients.Where(item => item.Type == type).Select(item => item.Address).ToArray();

    private static MailFileRecipientType MapRecipientType(EmailRecipientKind kind) => kind switch {
        EmailRecipientKind.To => MailFileRecipientType.To,
        EmailRecipientKind.Cc => MailFileRecipientType.Cc,
        EmailRecipientKind.Bcc => MailFileRecipientType.Bcc,
        EmailRecipientKind.ReplyTo => MailFileRecipientType.ReplyTo,
        EmailRecipientKind.Resource => MailFileRecipientType.Resource,
        EmailRecipientKind.Room => MailFileRecipientType.Room,
        _ => MailFileRecipientType.Unknown
    };

    private static IReadOnlyDictionary<string, string> MergeHeaders(IEnumerable<EmailHeader> headers) {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (EmailHeader header in headers) {
            if (result.TryGetValue(header.Name, out string? existing)) {
                result[header.Name] = string.Concat(existing, ", ", header.Value);
            } else {
                result[header.Name] = header.Value;
            }
        }
        return result;
    }

    private static EmailReaderOptions ResolveOfficeOptions(MailFileReaderOptions options) =>
        options.OfficeReaderOptions ?? new EmailReaderOptions(
            includeAttachmentContent: options.IncludeAttachments && options.IncludeAttachmentContent);

    private static MimeMessage LoadMimeMessage(string path, ParserOptions? options) => options == null
        ? MimeMessage.Load(path)
        : MimeMessage.Load(options, path);

    private static Task<MimeMessage> LoadMimeMessageAsync(string path, ParserOptions? options,
        CancellationToken cancellationToken) => options == null
        ? MimeMessage.LoadAsync(path, cancellationToken)
        : MimeMessage.LoadAsync(options, path, cancellationToken);

    private static void ValidateFile(FileInfo fileInfo) {
        if (fileInfo == null) throw new ArgumentNullException(nameof(fileInfo));
        if (!fileInfo.Exists) throw new FileNotFoundException("Mail file not found.", fileInfo.FullName);
    }

    private static MailFileFormat ResolveFormat(FileInfo fileInfo) {
        if (fileInfo.Extension.Equals(".msg", StringComparison.OrdinalIgnoreCase)) return MailFileFormat.Msg;
        if (fileInfo.Extension.Equals(".eml", StringComparison.OrdinalIgnoreCase)) return MailFileFormat.Eml;
        throw new NotSupportedException($"Unsupported mail file extension '{fileInfo.Extension}'.");
    }

    private static void EnsureExpectedFormat(EmailDocument document, MailFileFormat format, string path) {
        bool valid = format == MailFileFormat.Msg
            ? document.Format == EmailFileFormat.OutlookMsg
            : document.Format == EmailFileFormat.Eml;
        if (!valid) throw new FormatException($"File '{path}' content does not match its {format} extension.");
    }
}
