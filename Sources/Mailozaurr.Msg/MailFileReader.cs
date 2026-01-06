using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using MsgReader.Mime;
using MsgReader.Mime.Header;
using MsgReader.Outlook;
using OutlookMessage = MsgReader.Outlook.Storage.Message;
using OutlookAttachment = MsgReader.Outlook.Storage.Attachment;

namespace Mailozaurr;

/// <summary>Reads MSG and EML files into MailFileMessage models.</summary>
public static class MailFileReader {
    /// <summary>Reads a mail file from a path.</summary>
    /// <param name="path">Path to the mail file.</param>
    /// <param name="options">Reader options.</param>
    /// <returns>Parsed mail file message.</returns>
    public static MailFileMessage Read(string path, MailFileReaderOptions? options = null) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));
        }

        return Read(new FileInfo(path), options);
    }

    /// <summary>Reads a mail file from a FileInfo instance.</summary>
    /// <param name="fileInfo">Mail file info.</param>
    /// <param name="options">Reader options.</param>
    /// <returns>Parsed mail file message.</returns>
    public static MailFileMessage Read(FileInfo fileInfo, MailFileReaderOptions? options = null) {
        if (fileInfo == null) {
            throw new ArgumentNullException(nameof(fileInfo));
        }

        if (!fileInfo.Exists) {
            throw new FileNotFoundException("Mail file not found.", fileInfo.FullName);
        }

        options ??= new MailFileReaderOptions();

        if (fileInfo.Extension.Equals(".msg", StringComparison.OrdinalIgnoreCase)) {
            return ReadMsg(fileInfo, options);
        }

        if (fileInfo.Extension.Equals(".eml", StringComparison.OrdinalIgnoreCase)) {
            return ReadEml(fileInfo, options);
        }

        throw new NotSupportedException($"Unsupported mail file extension '{fileInfo.Extension}'.");
    }

    /// <summary>Attempts to read a mail file and returns an error message on failure.</summary>
    /// <param name="path">Path to the mail file.</param>
    /// <param name="message">Parsed mail file message when successful.</param>
    /// <param name="error">Error message when reading fails.</param>
    /// <param name="options">Reader options.</param>
    /// <returns>True when reading succeeds; otherwise false.</returns>
    public static bool TryRead(string path, out MailFileMessage? message, out string? error, MailFileReaderOptions? options = null) {
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

    private static MailFileMessage ReadMsg(FileInfo fileInfo, MailFileReaderOptions options) {
        using var message = new OutlookMessage(fileInfo.FullName);

        var recipients = BuildRecipients(message.Recipients);
        var to = recipients.Where(r => r.Type == MailFileRecipientType.To).Select(r => r.Address).ToList();
        var cc = recipients.Where(r => r.Type == MailFileRecipientType.Cc).Select(r => r.Address).ToList();
        var bcc = recipients.Where(r => r.Type == MailFileRecipientType.Bcc).Select(r => r.Address).ToList();

        MailFileAddress? from = null;
        if (message.Sender != null) {
            from = CreateAddress(message.Sender.Email, message.Sender.DisplayName, message.Sender.Raw);
        }

        var headers = options.IncludeHeaders ? ExtractHeaders(message.Headers) : null;
        var attachments = options.IncludeAttachments ? BuildMsgAttachments(message.Attachments, options.IncludeAttachmentContent) : Array.Empty<MailFileAttachment>();
        var headerMessageId = message.Headers?.MessageId;
        var messageId = !string.IsNullOrWhiteSpace(headerMessageId) ? headerMessageId : message.Id;

        return new MailFileMessage {
            Format = MailFileFormat.Msg,
            FilePath = fileInfo.FullName,
            Subject = message.Subject,
            From = from,
            To = to,
            Cc = cc,
            Bcc = bcc,
            Recipients = recipients,
            SentOn = message.SentOn,
            ReceivedOn = message.ReceivedOn,
            BodyText = message.BodyText,
            BodyHtml = message.BodyHtml,
            Attachments = attachments,
            MessageId = messageId,
            SignatureIsValid = message.SignatureIsValid,
            SignedBy = message.SignedBy,
            SignedOn = message.SignedOn,
            Headers = headers
        };
    }

    private static MailFileMessage ReadEml(FileInfo fileInfo, MailFileReaderOptions options) {
        var message = Message.Load(fileInfo);
        var headers = message.Headers;

        var recipients = new List<MailFileRecipient>();
        var to = BuildRecipients(headers?.To, MailFileRecipientType.To, recipients);
        var cc = BuildRecipients(headers?.Cc, MailFileRecipientType.Cc, recipients);
        var bcc = BuildRecipients(headers?.Bcc, MailFileRecipientType.Bcc, recipients);

        var sentOn = headers != null && headers.DateSent != DateTimeOffset.MinValue
            ? headers.DateSent
            : (DateTimeOffset?)null;

        var from = CreateAddress(headers?.From);
        var attachments = options.IncludeAttachments ? BuildEmlAttachments(message.Attachments, options.IncludeAttachmentContent) : Array.Empty<MailFileAttachment>();

        return new MailFileMessage {
            Format = MailFileFormat.Eml,
            FilePath = fileInfo.FullName,
            Subject = headers?.Subject,
            From = from,
            To = to,
            Cc = cc,
            Bcc = bcc,
            Recipients = recipients,
            SentOn = sentOn,
            BodyText = DecodeBody(message.TextBody),
            BodyHtml = DecodeBody(message.HtmlBody),
            Attachments = attachments,
            MessageId = headers?.MessageId,
            SignatureIsValid = message.SignatureIsValid,
            SignedBy = message.SignedBy,
            SignedOn = message.SignedOn,
            Headers = options.IncludeHeaders ? ExtractHeaders(headers) : null
        };
    }

    private static List<MailFileRecipient> BuildRecipients(List<MsgReader.Outlook.Storage.Recipient>? recipients) {
        var result = new List<MailFileRecipient>();
        if (recipients == null || recipients.Count == 0) {
            return result;
        }

        foreach (var recipient in recipients) {
            var address = CreateAddress(recipient.Email, recipient.DisplayName, recipient.Raw);
            if (address == null) {
                continue;
            }

            var type = recipient.Type.HasValue ? MapRecipientType(recipient.Type.Value) : MailFileRecipientType.Unknown;
            result.Add(new MailFileRecipient(type, address));
        }

        return result;
    }

    private static List<MailFileAddress> BuildRecipients(IEnumerable<RfcMailAddress>? addresses, MailFileRecipientType type, List<MailFileRecipient> recipients) {
        var list = new List<MailFileAddress>();
        if (addresses == null) {
            return list;
        }

        foreach (var address in addresses) {
            var mapped = CreateAddress(address);
            if (mapped == null) {
                continue;
            }

            list.Add(mapped);
            recipients.Add(new MailFileRecipient(type, mapped));
        }

        return list;
    }

    private static MailFileRecipientType MapRecipientType(MsgReader.Outlook.RecipientType type) {
        return type switch {
            MsgReader.Outlook.RecipientType.To => MailFileRecipientType.To,
            MsgReader.Outlook.RecipientType.Cc => MailFileRecipientType.Cc,
            MsgReader.Outlook.RecipientType.Bcc => MailFileRecipientType.Bcc,
            MsgReader.Outlook.RecipientType.Resource => MailFileRecipientType.Resource,
            MsgReader.Outlook.RecipientType.Room => MailFileRecipientType.Room,
            _ => MailFileRecipientType.Unknown
        };
    }

    private static MailFileAddress? CreateAddress(RfcMailAddress? address) {
        if (address == null) {
            return null;
        }

        return CreateAddress(address.Address, address.DisplayName, address.Raw);
    }

    private static MailFileAddress? CreateAddress(string? address, string? displayName, string? raw) {
        if (string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(raw)) {
            return null;
        }

        return new MailFileAddress(address, displayName, raw);
    }

    private static string? DecodeBody(MessagePart? part) {
        if (part == null || part.Body == null || part.Body.Length == 0) {
            return null;
        }

        var encoding = part.BodyEncoding ?? Encoding.UTF8;
        try {
            return encoding.GetString(part.Body);
        } catch (DecoderFallbackException) {
            return Encoding.UTF8.GetString(part.Body);
        } catch (ArgumentException) {
            return Encoding.UTF8.GetString(part.Body);
        }
    }

    private static IReadOnlyList<MailFileAttachment> BuildMsgAttachments(List<object>? attachments, bool includeContent) {
        if (attachments == null || attachments.Count == 0) {
            return Array.Empty<MailFileAttachment>();
        }

        var results = new List<MailFileAttachment>();
        foreach (var attachmentObj in attachments) {
            if (attachmentObj is OutlookAttachment attachment) {
                byte[]? content = includeContent ? attachment.Data : null;
                results.Add(new MailFileAttachment {
                    FileName = attachment.FileName,
                    ContentType = attachment.MimeType,
                    ContentId = attachment.ContentId,
                    IsInline = attachment.IsInline,
                    Size = attachment.Data?.LongLength,
                    Content = content
                });
                continue;
            }

            // Ignore unknown attachment types to stay AOT-friendly (no reflection).
        }

        return results;
    }

    private static IReadOnlyList<MailFileAttachment> BuildEmlAttachments(IEnumerable<MessagePart>? attachments, bool includeContent) {
        if (attachments == null) {
            return Array.Empty<MailFileAttachment>();
        }

        var results = new List<MailFileAttachment>();
        foreach (var attachment in attachments) {
            var body = includeContent ? attachment.Body : null;
            results.Add(new MailFileAttachment {
                FileName = ResolveAttachmentFileName(attachment),
                ContentType = attachment.ContentType?.ToString(),
                ContentId = attachment.ContentId,
                IsInline = attachment.IsInline,
                Size = attachment.Body?.LongLength,
                Content = body
            });
        }

        return results;
    }

    private static string? ResolveAttachmentFileName(MessagePart attachment) {
        if (!string.IsNullOrWhiteSpace(attachment.FileName)) {
            return attachment.FileName;
        }

        var contentDispositionFileName = attachment.ContentDisposition?.FileName;
        if (!string.IsNullOrWhiteSpace(contentDispositionFileName)) {
            return contentDispositionFileName;
        }

        var contentTypeName = attachment.ContentType?.Name;
        if (!string.IsNullOrWhiteSpace(contentTypeName)) {
            return contentTypeName;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string>? ExtractHeaders(MessageHeader? headers) {
        if (headers == null) {
            return null;
        }

        return MergeHeaders(headers.RawHeaders, headers.UnknownHeaders);
    }

    private static IReadOnlyDictionary<string, string> MergeHeaders(NameValueCollection? headers, NameValueCollection? unknownHeaders) {
        var count = (headers?.Count ?? 0) + (unknownHeaders?.Count ?? 0);
        var result = count > 0
            ? new Dictionary<string, string>(count, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddHeaders(result, headers);
        AddHeaders(result, unknownHeaders);
        return result;
    }

    private static void AddHeaders(IDictionary<string, string> destination, NameValueCollection? headers) {
        if (headers == null) {
            return;
        }

        foreach (var key in headers.AllKeys) {
            if (string.IsNullOrWhiteSpace(key)) {
                continue;
            }

            var value = headers[key];
            if (string.IsNullOrEmpty(value)) {
                continue;
            }

            if (destination.TryGetValue(key, out var existing)) {
                destination[key] = string.Concat(existing, ", ", value);
            } else {
                destination[key] = value;
            }
        }
    }

}
