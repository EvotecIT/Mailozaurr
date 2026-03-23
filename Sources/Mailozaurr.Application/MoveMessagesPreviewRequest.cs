namespace Mailozaurr.Application;

/// <summary>
/// Request for previewing a message move without executing it.
/// </summary>
public sealed class MoveMessagesPreviewRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional mailbox identifier for multi-mailbox providers.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional source folder identifier or folder path.</summary>
    public string? FolderId { get; set; }

    /// <summary>Provider-specific message identifiers to preview.</summary>
    public List<string> MessageIds { get; set; } = new();

    /// <summary>Requested destination folder identifier or provider alias.</summary>
    public string DestinationFolderId { get; set; } = string.Empty;
}
