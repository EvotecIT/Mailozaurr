using MimeKit;
using MimeKit.Utils;

namespace Mailozaurr.Application;

/// <summary>
/// Converts normalized draft messages into MIME messages that can be reused across send providers.
/// </summary>
public sealed class DraftMimeMessageFactory : IDraftMimeMessageFactory {
    /// <inheritdoc />
    public Task<MimeMessage> CreateAsync(MailProfile profile, DraftMessage draft, CancellationToken cancellationToken = default) {
        if (profile == null) {
            throw new ArgumentNullException(nameof(profile));
        }
        if (draft == null) {
            throw new ArgumentNullException(nameof(draft));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var message = new MimeMessage {
            Subject = draft.Subject ?? string.Empty,
            Date = DateTimeOffset.UtcNow,
            MessageId = MimeUtils.GenerateMessageId()
        };

        AddSender(message, profile, draft);
        AddRecipients(message.To, draft.To);
        AddRecipients(message.Cc, draft.Cc);
        AddRecipients(message.Bcc, draft.Bcc);
        AddRecipients(message.ReplyTo, draft.ReplyTo);
        if (!message.To.Any() && !message.Cc.Any() && !message.Bcc.Any()) {
            throw new InvalidOperationException("Draft message requires at least one recipient.");
        }

        AddHeaders(message, draft.Headers);
        message.Body = BuildBody(draft, cancellationToken);
        return Task.FromResult(message);
    }

    private static void AddSender(MimeMessage message, MailProfile profile, DraftMessage draft) {
        var sender = draft.From;
        if (sender != null) {
            message.From.Add(CreateMailboxAddress(sender));
            return;
        }

        if (!string.IsNullOrWhiteSpace(profile.DefaultSender)) {
            message.From.Add(MailboxAddress.Parse(profile.DefaultSender!.Trim()));
            return;
        }

        if (!string.IsNullOrWhiteSpace(profile.DefaultMailbox)) {
            message.From.Add(MailboxAddress.Parse(profile.DefaultMailbox!.Trim()));
            return;
        }

        throw new InvalidOperationException($"Profile '{profile.Id}' does not define a sender and the draft does not specify one.");
    }

    private static void AddRecipients(InternetAddressList target, IEnumerable<MessageRecipient> recipients) {
        if (target == null) {
            throw new ArgumentNullException(nameof(target));
        }
        if (recipients == null) {
            return;
        }

        foreach (var recipient in recipients) {
            if (recipient == null || string.IsNullOrWhiteSpace(recipient.Address)) {
                continue;
            }

            target.Add(CreateMailboxAddress(recipient));
        }
    }

    private static MailboxAddress CreateMailboxAddress(MessageRecipient recipient) {
        if (recipient == null) {
            throw new ArgumentNullException(nameof(recipient));
        }
        if (string.IsNullOrWhiteSpace(recipient.Address)) {
            throw new InvalidOperationException("Recipient address cannot be empty.");
        }

        return string.IsNullOrWhiteSpace(recipient.Name)
            ? MailboxAddress.Parse(recipient.Address.Trim())
            : new MailboxAddress(recipient.Name!.Trim(), recipient.Address.Trim());
    }

    private static void AddHeaders(MimeMessage message, IReadOnlyDictionary<string, string> headers) {
        if (headers == null || headers.Count == 0) {
            return;
        }

        foreach (var header in headers) {
            if (string.IsNullOrWhiteSpace(header.Key) || string.IsNullOrWhiteSpace(header.Value)) {
                continue;
            }

            message.Headers.Add(header.Key.Trim(), header.Value.Trim());
        }
    }

    private static MimeEntity BuildBody(DraftMessage draft, CancellationToken cancellationToken) {
        var bodyBuilder = new BodyBuilder();
        if (!string.IsNullOrWhiteSpace(draft.TextBody)) {
            bodyBuilder.TextBody = draft.TextBody;
        }
        if (!string.IsNullOrWhiteSpace(draft.HtmlBody)) {
            bodyBuilder.HtmlBody = draft.HtmlBody;
        }

        foreach (var attachment in draft.Attachments) {
            cancellationToken.ThrowIfCancellationRequested();
            if (attachment == null || string.IsNullOrWhiteSpace(attachment.Path)) {
                continue;
            }

            var entity = CreateAttachmentEntity(attachment);
            if (attachment.IsInline) {
                bodyBuilder.LinkedResources.Add(entity);
            } else {
                bodyBuilder.Attachments.Add(entity);
            }
        }

        return bodyBuilder.ToMessageBody();
    }

    private static MimeEntity CreateAttachmentEntity(DraftAttachment attachment) {
        var fullPath = Path.GetFullPath(attachment.Path);
        if (!File.Exists(fullPath)) {
            throw new FileNotFoundException($"Attachment '{attachment.Path}' was not found.", fullPath);
        }

        var fileName = string.IsNullOrWhiteSpace(attachment.FileName)
            ? Path.GetFileName(fullPath)
            : attachment.FileName!.Trim();
        var contentType = ResolveContentType(attachment, fileName);
        var part = new MimePart(contentType) {
            Content = new MimeContent(new MemoryStream(File.ReadAllBytes(fullPath), writable: false)),
            FileName = fileName,
            ContentDisposition = new ContentDisposition(attachment.IsInline ? ContentDisposition.Inline : ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64
        };

        if (attachment.IsInline) {
            part.ContentId = string.IsNullOrWhiteSpace(attachment.ContentId)
                ? MimeUtils.GenerateMessageId()
                : attachment.ContentId!.Trim();
        }

        return part;
    }

    private static ContentType ResolveContentType(DraftAttachment attachment, string fileName) {
        if (!string.IsNullOrWhiteSpace(attachment.ContentType)) {
            return ContentType.Parse(attachment.ContentType!.Trim());
        }

        return ContentType.Parse(MimeTypes.GetMimeType(fileName));
    }
}