namespace Mailozaurr.Application;

/// <summary>
/// Request for moving one or more messages.
/// </summary>
public sealed class MoveMessagesRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional mailbox identifier for multi-mailbox providers.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional source folder identifier or folder path.</summary>
    public string? FolderId { get; set; }

    /// <summary>Provider-specific message identifiers to move.</summary>
    public List<string> MessageIds { get; set; } = new();

    /// <summary>Destination folder identifier or provider alias.</summary>
    public string DestinationFolderId { get; set; } = string.Empty;

    /// <summary>Optional confirmation token from a prior preview for this exact action.</summary>
    public string? ConfirmationToken { get; set; }
}