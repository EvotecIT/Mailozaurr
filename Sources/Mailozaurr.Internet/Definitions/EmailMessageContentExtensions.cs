namespace Mailozaurr;

using Mailozaurr.Definitions;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Applies transport-neutral rendered message content to Mailozaurr transport clients.
/// Envelope, authentication, retry, and provider settings remain owned by the caller.
/// </summary>
public static class EmailMessageContentExtensions {
    /// <summary>Applies rendered content to an SMTP client.</summary>
    public static Smtp WithContent(this Smtp client, EmailMessageContent content) {
        if (client == null) throw new ArgumentNullException(nameof(client));
        ValidateContent(content);
        client.Subject = content.Subject;
        client.HtmlBody = content.HtmlBody;
        client.TextBody = content.TextBody;
        client.Attachments = Copy(content.Attachments);
        client.InlineAttachments = Copy(content.InlineAttachments);
        client.Headers = CopyHeaders(content);
        return client;
    }

    /// <summary>Applies rendered content to a SendGrid client.</summary>
    public static SendGridClient WithContent(this SendGridClient client, EmailMessageContent content) {
        if (client == null) throw new ArgumentNullException(nameof(client));
        ValidateContent(content);
        client.Subject = content.Subject;
        client.Html = content.HtmlBody;
        client.Text = content.TextBody;
        var attachments = Copy(content.Attachments);
        var inlineAttachments = Copy(content.InlineAttachments);
        client.Attachments = attachments.Count == 0 ? null : attachments;
        client.InlineAttachments = inlineAttachments.Count == 0 ? null : inlineAttachments;
        client.Headers = CopyHeaders(content);
        return client;
    }

    /// <summary>Applies rendered content to a Mailgun client.</summary>
    public static MailgunClient WithContent(this MailgunClient client, EmailMessageContent content) {
        if (client == null) throw new ArgumentNullException(nameof(client));
        ValidateContent(content);
        client.Subject = content.Subject;
        client.Html = content.HtmlBody;
        client.Text = content.TextBody;
        client.Attachments = Copy(content.Attachments);
        client.InlineAttachments = Copy(content.InlineAttachments);
        client.Attachment = null;
        client.InlineAttachment = null;
        client.Headers = CopyHeaders(content);
        return client;
    }

    /// <summary>Applies rendered content to an Amazon SES client.</summary>
    public static SesClient WithContent(this SesClient client, EmailMessageContent content) {
        if (client == null) throw new ArgumentNullException(nameof(client));
        ValidateContent(content);
        client.Subject = content.Subject;
        client.Html = content.HtmlBody;
        client.Text = content.TextBody;
        client.Attachments = Copy(content.Attachments);
        client.InlineAttachments = Copy(content.InlineAttachments);
        client.Attachment = null;
        client.InlineAttachment = null;
        client.Headers = CopyHeaders(content);
        return client;
    }

    private static void ValidateContent(EmailMessageContent content) {
        if (content == null) throw new ArgumentNullException(nameof(content));
    }

    private static List<AttachmentDescriptor> Copy(IEnumerable<AttachmentDescriptor> attachments) =>
        attachments?.Where(item => item != null).ToList()
        ?? new List<AttachmentDescriptor>();

    private static Dictionary<string, string>? CopyHeaders(EmailMessageContent content) =>
        content.Headers.Count == 0
            ? null
            : new Dictionary<string, string>(content.Headers, StringComparer.OrdinalIgnoreCase);

}
