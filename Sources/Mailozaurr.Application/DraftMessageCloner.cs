namespace Mailozaurr.Application;

/// <summary>
/// Creates detached copies of reusable draft messages.
/// </summary>
public static class DraftMessageCloner {
    /// <summary>
    /// Creates a deep copy of a draft and its mutable recipient, header, and
    /// attachment collections.
    /// </summary>
    /// <param name="draft">Draft to copy.</param>
    /// <returns>A detached draft copy.</returns>
    public static DraftMessage Clone(DraftMessage draft) {
        if (draft == null) {
            throw new ArgumentNullException(nameof(draft));
        }

        return new DraftMessage {
            ProfileId = draft.ProfileId,
            From = draft.From == null ? null : CloneRecipient(draft.From),
            To = draft.To.Select(CloneRecipient).ToList(),
            Cc = draft.Cc.Select(CloneRecipient).ToList(),
            Bcc = draft.Bcc.Select(CloneRecipient).ToList(),
            ReplyTo = draft.ReplyTo.Select(CloneRecipient).ToList(),
            Subject = draft.Subject,
            TextBody = draft.TextBody,
            HtmlBody = draft.HtmlBody,
            Priority = draft.Priority,
            Headers = new Dictionary<string, string>(
                draft.Headers,
                StringComparer.OrdinalIgnoreCase),
            Attachments = draft.Attachments.Select(CloneAttachment).ToList()
        };
    }

    private static MessageRecipient CloneRecipient(
        MessageRecipient recipient) => new() {
        Name = recipient.Name,
        Address = recipient.Address
    };

    private static DraftAttachment CloneAttachment(
        DraftAttachment attachment) => new() {
        Path = attachment.Path,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        IsInline = attachment.IsInline,
        ContentId = attachment.ContentId
    };
}
