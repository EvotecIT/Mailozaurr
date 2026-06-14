namespace Mailozaurr.Application;

/// <summary>
/// Request for previewing a common bundle of message actions side by side.
/// </summary>
public sealed class CommonMessageActionsPreviewRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional mailbox identifier for multi-mailbox providers.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional source folder identifier or folder path.</summary>
    public string? FolderId { get; set; }

    /// <summary>Provider-specific message identifiers to preview.</summary>
    public List<string> MessageIds { get; set; } = new();

    /// <summary>Optional custom destination folder identifier or alias to include as a generic move preview.</summary>
    public string? DestinationFolderId { get; set; }
}