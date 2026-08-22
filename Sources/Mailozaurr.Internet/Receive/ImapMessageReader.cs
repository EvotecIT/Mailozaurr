using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Request model for reading a single IMAP message by UID.
/// </summary>
public sealed record ImapMessageReadRequest(
    UniqueId Uid,
    string? Folder,
    long MaxBodyBytes);

/// <summary>
/// Attachment projection returned from <see cref="ImapMessageReader"/>.
/// </summary>
public sealed record ImapMessageAttachmentInfo(
    string FileName,
    string ContentType);

/// <summary>
/// Flattened IMAP message projection returned from <see cref="ImapMessageReader"/>.
/// </summary>
public sealed record ImapMessageReadResult(
    long Uid,
    string Folder,
    string Subject,
    string From,
    string To,
    DateTimeOffset DateUtc,
    string TextBody,
    bool TextTruncated,
    string HtmlBody,
    bool HtmlTruncated,
    bool HasAttachments,
    IReadOnlyList<ImapMessageAttachmentInfo> Attachments);

/// <summary>
/// Reads a single IMAP message with reusable folder resolution and truncation semantics.
/// </summary>
public static class ImapMessageReader {
    /// <summary>
    /// Reads and projects a single IMAP message by UID.
    /// </summary>
    public static async Task<ImapMessageReadResult> ReadAsync(
        ImapClient client,
        ImapMessageReadRequest request,
        CancellationToken cancellationToken = default) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var mailFolder = client.GetCachedFolder(request.Folder, FolderAccess.ReadOnly);
        var message = await mailFolder.GetMessageAsync(request.Uid, cancellationToken).ConfigureAwait(false);

        var attachments = message.Attachments.Select(static attachment => new ImapMessageAttachmentInfo(
            FileName: attachment switch {
                MimePart part => part.FileName ?? string.Empty,
                MessagePart messagePart => messagePart.ContentDisposition?.FileName
                    ?? messagePart.ContentType?.Name
                    ?? string.Empty,
                _ => string.Empty
            },
            ContentType: attachment.ContentType?.MimeType ?? string.Empty)).ToArray();

        var text = TruncateUtf8(message.TextBody, request.MaxBodyBytes, out var textTruncated);
        var html = TruncateUtf8(message.HtmlBody, request.MaxBodyBytes, out var htmlTruncated);

        return new ImapMessageReadResult(
            Uid: request.Uid.Id,
            Folder: mailFolder.FullName,
            Subject: message.Subject ?? string.Empty,
            From: string.Join(", ", message.From.Mailboxes.Select(static mailbox => mailbox.ToString())),
            To: string.Join(", ", message.To.Mailboxes.Select(static mailbox => mailbox.ToString())),
            DateUtc: message.Date.ToUniversalTime(),
            TextBody: text ?? string.Empty,
            TextTruncated: textTruncated,
            HtmlBody: html ?? string.Empty,
            HtmlTruncated: htmlTruncated,
            HasAttachments: attachments.Length > 0,
            Attachments: attachments);
    }

    private static string? TruncateUtf8(string? value, long maxBytes, out bool truncated) {
        truncated = false;
        if (string.IsNullOrEmpty(value)) {
            return value;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.LongLength <= maxBytes) {
            return value;
        }

        truncated = true;
        var truncatedBytes = new byte[(int)Math.Min(maxBytes, int.MaxValue)];
        Array.Copy(bytes, truncatedBytes, truncatedBytes.Length);
        return Encoding.UTF8.GetString(truncatedBytes);
    }
}
