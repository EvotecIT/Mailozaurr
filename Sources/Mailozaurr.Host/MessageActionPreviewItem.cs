namespace Mailozaurr.Hosting;

/// <summary>
/// Reusable dry-run preview entry for a single mailbox action.
/// </summary>
public sealed class MessageActionPreviewItem : OperationResult {
    /// <summary>Stable action name such as archive, trash, move, or delete.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Human-readable label for the action preview.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The requested destination folder or alias when relevant.</summary>
    public string? RequestedDestinationFolderId { get; set; }

    /// <summary>The resolved folder target when the action uses a destination.</summary>
    public MailFolderTargetResolution? Destination { get; set; }

    /// <summary>Desired state value when the action is a state change.</summary>
    public bool? DesiredState { get; set; }

    /// <summary>Optional confirmation token for executing this exact action preview.</summary>
    public string? ConfirmationToken { get; set; }

    /// <summary>Warnings detected while building this action preview.</summary>
    public List<string> Warnings { get; set; } = new();
}