namespace Mailozaurr.Application;

/// <summary>
/// Aggregate dry-run result for a planned message state change such as read/unread or flag/unflag.
/// </summary>
public sealed class MessageStateChangePreview : OperationResult {
    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Owning mailbox identifier when relevant.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional source folder identifier.</summary>
    public string? FolderId { get; set; }

    /// <summary>Stable action name such as read-state or flagged-state.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>The desired state value for the action.</summary>
    public bool DesiredState { get; set; }

    /// <summary>Total raw message identifiers provided in the request.</summary>
    public int RequestedCount { get; set; }

    /// <summary>Total unique, non-empty message identifiers after normalization.</summary>
    public int UniqueMessageCount { get; set; }

    /// <summary>Total duplicate or empty message identifiers removed during normalization.</summary>
    public int DuplicateOrEmptyCount { get; set; }

    /// <summary>The normalized unique message identifiers that would be acted on.</summary>
    public List<string> MessageIds { get; set; } = new();

    /// <summary>Optional confirmation token that can be supplied when executing the action.</summary>
    public string? ConfirmationToken { get; set; }

    /// <summary>Warnings detected during preview.</summary>
    public List<string> Warnings { get; set; } = new();
}
