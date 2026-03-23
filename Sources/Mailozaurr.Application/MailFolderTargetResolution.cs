namespace Mailozaurr.Application;

/// <summary>
/// Describes how a requested folder target resolves for a specific profile and mailbox.
/// </summary>
public sealed class MailFolderTargetResolution {
    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Owning mailbox identifier when relevant.</summary>
    public string? MailboxId { get; set; }

    /// <summary>The original requested destination folder value.</summary>
    public string RequestedValue { get; set; } = string.Empty;

    /// <summary>True when the requested value matched a known provider-neutral alias.</summary>
    public bool IsAlias { get; set; }

    /// <summary>The canonical provider-neutral alias when <see cref="IsAlias" /> is true.</summary>
    public string? Alias { get; set; }

    /// <summary>True when the target is supported for the selected profile.</summary>
    public bool IsSupported { get; set; }

    /// <summary>True when a provider-specific folder target was resolved.</summary>
    public bool IsResolved { get; set; }

    /// <summary>The effective folder identifier that should be used for actions.</summary>
    public string EffectiveFolderId { get; set; } = string.Empty;

    /// <summary>The resolved provider folder display name when known.</summary>
    public string? FolderDisplayName { get; set; }

    /// <summary>The resolved provider folder path when known.</summary>
    public string? FolderPath { get; set; }

    /// <summary>The resolved provider special-use marker when known.</summary>
    public string? SpecialUse { get; set; }

    /// <summary>Human-readable summary for lightweight CLI and MCP output.</summary>
    public string Summary { get; set; } = string.Empty;
}
