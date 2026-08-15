namespace Mailozaurr.Hosting;

/// <summary>
/// Lightweight projection of a folder or folder-like mailbox container.
/// </summary>
public sealed class FolderRefCompact {
    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Owning mailbox identifier when relevant.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Provider-specific folder identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>User-facing folder name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Full path or canonical folder name.</summary>
    public string? Path { get; set; }

    /// <summary>Provider-specific special use marker such as Inbox or Sent.</summary>
    public string? SpecialUse { get; set; }

    /// <summary>Total message count when known.</summary>
    public int? MessageCount { get; set; }

    /// <summary>Unread message count when known.</summary>
    public int? UnreadCount { get; set; }

    /// <summary>Short human-readable summary line.</summary>
    public string Summary { get; set; } = string.Empty;
}