namespace Mailozaurr.Hosting;

/// <summary>
/// Request for saving an attachment.
/// </summary>
public sealed class SaveAttachmentRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional mailbox identifier for multi-mailbox providers.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional folder identifier or folder path.</summary>
    public string? FolderId { get; set; }

    /// <summary>Provider-specific message identifier.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Provider-specific attachment identifier.</summary>
    public string AttachmentId { get; set; } = string.Empty;

    /// <summary>Destination path.</summary>
    public string DestinationPath { get; set; } = string.Empty;

    /// <summary>Whether existing files may be overwritten.</summary>
    public bool Overwrite { get; set; }
}