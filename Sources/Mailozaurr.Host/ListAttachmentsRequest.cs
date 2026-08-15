namespace Mailozaurr.Hosting;

/// <summary>
/// Request for listing attachments associated with a message.
/// </summary>
public sealed class ListAttachmentsRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional mailbox identifier for multi-mailbox providers.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional folder identifier when the provider requires folder scoping.</summary>
    public string? FolderId { get; set; }

    /// <summary>Provider-specific message identifier.</summary>
    public string MessageId { get; set; } = string.Empty;
}