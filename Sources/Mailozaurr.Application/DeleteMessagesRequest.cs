namespace Mailozaurr.Application;

/// <summary>
/// Request for deleting one or more messages.
/// </summary>
public sealed class DeleteMessagesRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional mailbox identifier for multi-mailbox providers.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional folder identifier or folder path.</summary>
    public string? FolderId { get; set; }

    /// <summary>Provider-specific message identifiers to delete.</summary>
    public List<string> MessageIds { get; set; } = new();

    /// <summary>Optional confirmation token from a prior preview for this exact action.</summary>
    public string? ConfirmationToken { get; set; }
}
