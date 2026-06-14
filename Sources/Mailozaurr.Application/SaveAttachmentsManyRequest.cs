namespace Mailozaurr.Application;

/// <summary>
/// Request for saving one or more attachments across multiple messages.
/// </summary>
public sealed class SaveAttachmentsManyRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional mailbox identifier for multi-mailbox providers.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional folder identifier or folder path.</summary>
    public string? FolderId { get; set; }

    /// <summary>Provider-specific message identifiers to inspect.</summary>
    public List<string> MessageIds { get; set; } = new();

    /// <summary>Destination path or directory.</summary>
    public string DestinationPath { get; set; } = string.Empty;

    /// <summary>Optional explicit attachment identifiers to include.</summary>
    public List<string> AttachmentIds { get; set; } = new();

    /// <summary>Optional case-insensitive file-name filter.</summary>
    public string? FileNameContains { get; set; }

    /// <summary>Optional case-insensitive content-type filter.</summary>
    public string? ContentTypeContains { get; set; }

    /// <summary>Whether existing files may be overwritten.</summary>
    public bool Overwrite { get; set; }
}