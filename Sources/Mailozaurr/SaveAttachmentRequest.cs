namespace Mailozaurr;

/// <summary>Describes how a save-attachment destination path is interpreted.</summary>
public enum AttachmentDestinationKind {
    /// <summary>Preserves compatibility by treating an existing directory as a directory and any other path as a file.</summary>
    Auto = 0,
    /// <summary>Treats the destination as an explicit output file.</summary>
    File = 1,
    /// <summary>Treats the destination as a directory, including when it does not exist yet.</summary>
    Directory = 2
}

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

    /// <summary>Controls whether <see cref="DestinationPath"/> is a file, a directory, or compatibility auto-detected.</summary>
    public AttachmentDestinationKind DestinationKind { get; set; } = AttachmentDestinationKind.Auto;

    /// <summary>Whether existing files may be overwritten.</summary>
    public bool Overwrite { get; set; }
}
