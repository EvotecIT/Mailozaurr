namespace Mailozaurr.Application;

/// <summary>
/// Request for listing folders or folder-like mailbox containers.
/// </summary>
public sealed class MailFolderQuery {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional mailbox identifier for multi-mailbox providers.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional parent folder identifier.</summary>
    public string? ParentFolderId { get; set; }

    /// <summary>Whether only top-level folders should be returned.</summary>
    public bool RootOnly { get; set; }
}