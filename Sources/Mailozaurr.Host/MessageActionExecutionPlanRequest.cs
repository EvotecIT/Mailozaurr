namespace Mailozaurr.Hosting;

/// <summary>
/// Request for creating a normalized execution plan from a selected message action.
/// </summary>
public sealed class MessageActionExecutionPlanRequest {
    /// <summary>Stable action name such as mark-read, mark-unread, flag, unflag, archive, trash, move, or delete.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional mailbox identifier for multi-mailbox providers.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional source folder identifier or folder path.</summary>
    public string? FolderId { get; set; }

    /// <summary>Provider-specific message identifiers to normalize for execution.</summary>
    public List<string> MessageIds { get; set; } = new();

    /// <summary>Optional destination folder identifier or alias when the action requires one.</summary>
    public string? DestinationFolderId { get; set; }

    /// <summary>Optional confirmation token returned by a matching preview call.</summary>
    public string? ConfirmationToken { get; set; }
}