using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;

namespace Mailozaurr;

/// <summary>
/// Helpers for preparing MimeKit messages for Microsoft Graph APIs.
/// </summary>
public static class GraphMimePreparation {
    /// <summary>
    /// Default maximum attachment bytes kept inline in Graph JSON payload.
    /// </summary>
    public const int DefaultMaxInlineAttachmentBytes = 3 * 1024 * 1024;

    /// <summary>
    /// Encodes bytes into base64url.
    /// </summary>
    public static string Base64UrlEncode(byte[] bytes) {
        var base64 = Convert.ToBase64String(bytes);
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Prepares a MIME message for Graph create-message APIs.
    /// </summary>
    public static GraphPreparedMessage PrepareMessage(
        MimeMessage message,
        int maxInlineAttachmentBytes = DefaultMaxInlineAttachmentBytes,
        string? idempotencyHeaderName = null) {
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }
        if (maxInlineAttachmentBytes < 0) {
            throw new ArgumentOutOfRangeException(nameof(maxInlineAttachmentBytes));
        }

        var inlineAttachments = new List<GraphAttachment>();
        var inlineDecodedAttachments = new List<DecodedMimeAttachment>();
        var uploadAttachments = new List<DecodedMimeAttachment>();
        var inlineBytes = 0L;

        try {
            foreach (var part in EnumerateAttachmentParts(message)) {
                var decoded = DecodedMimeAttachment.DecodeToTempFile(part);
                if (decoded.Length <= maxInlineAttachmentBytes &&
                    inlineBytes + decoded.Length <= maxInlineAttachmentBytes) {
                    inlineBytes += decoded.Length;
                    inlineDecodedAttachments.Add(decoded);
                    inlineAttachments.Add(decoded.ToGraphAttachment());
                } else {
                    uploadAttachments.Add(decoded);
                }
            }

            var graphMessage = ConvertToGraphMessage(
                message,
                inlineAttachments.Count == 0 ? null : inlineAttachments,
                idempotencyHeaderName);

            while (GetSerializedMessageSize(graphMessage) > 4_000_000 && inlineAttachments.Count > 0) {
                var last = inlineAttachments.Count - 1;
                inlineAttachments.RemoveAt(last);
                uploadAttachments.Insert(0, inlineDecodedAttachments[last]);
                inlineDecodedAttachments.RemoveAt(last);
                graphMessage.Attachments = inlineAttachments.Count == 0 ? null : inlineAttachments;
            }

            if (GetSerializedMessageSize(graphMessage) > 4_000_000) {
                throw new InvalidOperationException("The serialized Graph draft exceeds the 4MB request limit without attachments. Reduce the message body, recipients, or headers.");
            }

            foreach (var decoded in inlineDecodedAttachments) {
                decoded.Dispose();
            }
            inlineDecodedAttachments.Clear();
            return new GraphPreparedMessage(graphMessage, uploadAttachments);
        } catch {
            foreach (var decoded in inlineDecodedAttachments) {
                decoded.Dispose();
            }
            foreach (var decoded in uploadAttachments) {
                decoded.Dispose();
            }
            throw;
        }
    }

    private static IEnumerable<MimePart> EnumerateAttachmentParts(MimeMessage message) {
        foreach (var part in message.BodyParts.OfType<MimePart>()) {
            var isInline = string.Equals(
                part.ContentDisposition?.Disposition,
                ContentDisposition.Inline,
                StringComparison.OrdinalIgnoreCase);
            if (part.IsAttachment || isInline && part is not TextPart) {
                yield return part;
            }
        }
    }

    private static int GetSerializedMessageSize(GraphMessage message) =>
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message, MailozaurrJsonContext.Default.GraphMessage).Length;

    /// <summary>
    /// Converts a MIME message to Graph message payload.
    /// </summary>
    public static GraphMessage ConvertToGraphMessage(
        MimeMessage message,
        List<GraphAttachment>? attachments = null,
        string? idempotencyHeaderName = null) {
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }

        var html = message.HtmlBody;
        var text = message.TextBody;
        var bodyContentType = string.IsNullOrWhiteSpace(html) ? "Text" : "HTML";
        var bodyContent = string.IsNullOrWhiteSpace(html) ? (text ?? string.Empty) : html!;

        var headers = new List<GraphInternetMessageHeader>();
        AddCustomHeaders(headers, message.Headers);

        if (idempotencyHeaderName != null) {
            var trimmedIdempotencyHeaderName = idempotencyHeaderName.Trim();
            if (trimmedIdempotencyHeaderName.Length > 0 &&
                trimmedIdempotencyHeaderName.StartsWith("x-", StringComparison.OrdinalIgnoreCase) &&
                !ContainsHeader(headers, trimmedIdempotencyHeaderName)) {
                AddHeader(headers, trimmedIdempotencyHeaderName, message.Headers[trimmedIdempotencyHeaderName]);
            }
        }

        return new GraphMessage {
            Subject = message.Subject ?? string.Empty,
            Body = new GraphContent { Type = bodyContentType, Content = bodyContent },
            To = ConvertRecipients(message.To),
            Cc = ConvertRecipients(message.Cc),
            Bcc = ConvertRecipients(message.Bcc),
            ReplyTo = ConvertRecipients(message.ReplyTo),
            Importance = message.Priority switch {
                MimeKit.MessagePriority.Urgent => "high",
                MimeKit.MessagePriority.NonUrgent => "low",
                _ => "normal"
            },
            InternetMessageHeaders = headers.Count == 0 ? null : headers,
            Attachments = attachments
        };
    }

    private static List<GraphEmailAddress>? ConvertRecipients(InternetAddressList? list) {
        if (list == null) {
            return null;
        }

        var recipients = new List<GraphEmailAddress>();
        foreach (var mailbox in list.Mailboxes) {
            var address = (mailbox.Address ?? string.Empty).Trim();
            if (address.Length == 0) {
                continue;
            }

            recipients.Add(new GraphEmailAddress {
                Email = new GraphEmail {
                    Address = address,
                    Name = string.IsNullOrWhiteSpace(mailbox.Name) ? null : mailbox.Name!.Trim()
                }
            });
        }

        return recipients.Count == 0 ? null : recipients;
    }

    private static void AddHeader(List<GraphInternetMessageHeader> headers, string name, string? value) {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0) {
            return;
        }

        headers.Add(new GraphInternetMessageHeader {
            Name = name,
            Value = trimmed
        });
    }

    private static void AddCustomHeaders(List<GraphInternetMessageHeader> headers, HeaderList headerList) {
        if (headerList == null) {
            return;
        }

        foreach (var header in headerList) {
            if (header == null ||
                string.IsNullOrWhiteSpace(header.Field) ||
                !header.Field.StartsWith("x-", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            AddHeader(headers, header.Field, header.Value);
        }
    }

    private static bool ContainsHeader(List<GraphInternetMessageHeader> headers, string name) {
        foreach (var header in headers) {
            if (string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Result of graph MIME preparation.
/// </summary>
public sealed record GraphPreparedMessage(
    GraphMessage Message,
    IReadOnlyList<DecodedMimeAttachment> UploadAttachments);

/// <summary>
/// Decoded attachment materialized into a temp file.
/// </summary>
public sealed class DecodedMimeAttachment : IDisposable {
    private readonly string _tempPath;

    private DecodedMimeAttachment(
        string tempPath,
        string name,
        string? contentType,
        bool isInline,
        string? contentId,
        long length) {
        _tempPath = tempPath;
        Name = name;
        ContentType = contentType;
        IsInline = isInline;
        ContentId = contentId;
        Length = length;
    }

    /// <summary>Attachment display name.</summary>
    public string Name { get; }
    /// <summary>Optional MIME content type.</summary>
    public string? ContentType { get; }
    /// <summary>True when attachment is inline.</summary>
    public bool IsInline { get; }
    /// <summary>Inline content id (cid).</summary>
    public string? ContentId { get; }
    /// <summary>Attachment length in bytes.</summary>
    public long Length { get; }

    /// <summary>
    /// Decodes a MIME part into a temp-file backed attachment.
    /// </summary>
    public static DecodedMimeAttachment DecodeToTempFile(MimePart part) {
        if (part == null) {
            throw new ArgumentNullException(nameof(part));
        }

        var fileName = (part.FileName ?? string.Empty).Trim();
        if (fileName.Length == 0) {
            fileName = "attachment";
        }

        var isInline = part.ContentDisposition?.Disposition?.Equals(ContentDisposition.Inline, StringComparison.OrdinalIgnoreCase) == true;
        var contentType = part.ContentType?.MimeType;
        var contentId = isInline ? part.ContentId : null;
        var tempPath = Path.Combine(Path.GetTempPath(), $"mailozaurr-mime-{Guid.NewGuid():N}.bin");

        try {
            using var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            if (part.Content != null) {
                part.Content.DecodeTo(fs);
            } else {
                part.WriteTo(fs);
            }
            fs.Flush(true);
            return new DecodedMimeAttachment(tempPath, fileName, contentType, isInline, contentId, fs.Length);
        } catch {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <summary>
    /// Opens attachment content as a readable stream.
    /// </summary>
    public Stream OpenRead() {
        return new FileStream(_tempPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    /// <summary>
    /// Reads all attachment bytes.
    /// </summary>
    public byte[] ReadAllBytes() {
        return File.ReadAllBytes(_tempPath);
    }

    /// <summary>Creates a Graph file-attachment payload from the decoded MIME content.</summary>
    public GraphAttachment ToGraphAttachment() {
        return new GraphAttachment {
            ODataType = "#microsoft.graph.fileAttachment",
            Name = Name,
            ContentType = ContentType,
            ContentBytes = Convert.ToBase64String(ReadAllBytes()),
            IsInline = IsInline,
            ContentId = IsInline ? ContentId : null
        };
    }

    /// <inheritdoc />
    public void Dispose() {
        TryDelete(_tempPath);
        GC.SuppressFinalize(this);
    }

    private static void TryDelete(string path) {
        try {
            if (File.Exists(path)) {
                File.Delete(path);
            }
        } catch {
            // best-effort cleanup
        }
    }
}
