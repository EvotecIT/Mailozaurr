namespace Mailozaurr;

/// <summary>
/// Describes a provider-neutral folder alias and its resolved provider target when known.
/// </summary>
public sealed class MailFolderAliasSummary {
    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Owning mailbox identifier when relevant.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Provider-neutral alias such as Inbox, Archive, or Trash.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>User-facing alias display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Indicates whether the alias can be used by the selected profile.</summary>
    public bool IsSupported { get; set; }

    /// <summary>Indicates whether the alias was resolved to a provider folder.</summary>
    public bool IsResolved { get; set; }

    /// <summary>Resolved provider folder identifier when known.</summary>
    public string? FolderId { get; set; }

    /// <summary>Resolved provider folder display name when known.</summary>
    public string? FolderDisplayName { get; set; }

    /// <summary>Resolved provider folder path when known.</summary>
    public string? FolderPath { get; set; }

    /// <summary>Resolved provider special-use marker when known.</summary>
    public string? SpecialUse { get; set; }

    /// <summary>Human-readable summary for lightweight CLI and MCP output.</summary>
    public string Summary { get; set; } = string.Empty;
}