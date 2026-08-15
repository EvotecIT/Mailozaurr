using Mailozaurr.Definitions;

namespace Mailozaurr;

/// <summary>Applies provider-neutral rendered content to Microsoft Graph mail.</summary>
public static class GraphEmailMessageContentExtensions {
    /// <summary>Applies rendered content while leaving Graph envelope and authentication settings unchanged.</summary>
    public static Graph WithContent(this Graph client, EmailMessageContent content) {
        if (client == null) throw new ArgumentNullException(nameof(client));
        if (content == null) throw new ArgumentNullException(nameof(content));
        client.Subject = content.Subject;
        if (!string.IsNullOrEmpty(content.HtmlBody)) {
            client.HTML = content.HtmlBody;
            client.ContentType = "HTML";
        } else {
            client.HTML = content.TextBody;
            client.ContentType = "Text";
        }
        var attachments = content.Attachments.Cast<object>().ToList();
        attachments.AddRange(content.InlineAttachments.Select(GraphAttachment.PrepareInlineDescriptor));
        client.Attachments = attachments.Count == 0 ? null : attachments.ToArray();
        client.Headers = content.Headers.Count == 0 ? null
            : new Dictionary<string, string>(content.Headers, StringComparer.OrdinalIgnoreCase);
        return client;
    }
}
