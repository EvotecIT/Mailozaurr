namespace Mailozaurr.Application;

/// <summary>
/// Normalized execution plan for a selected message action.
/// </summary>
public sealed class MessageActionExecutionPlan : OperationResult {
    /// <summary>Human-readable plan name suitable for selection in stored batches.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human-readable plan summary suitable for list-style displays.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Stable action name such as mark-read, mark-unread, flag, unflag, archive, trash, move, or delete.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Normalized execution kind such as SetReadState, SetFlaggedState, Move, or Delete.</summary>
    public string ExecutionKind { get; set; } = string.Empty;

    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Owning mailbox identifier when relevant.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional source folder identifier.</summary>
    public string? FolderId { get; set; }

    /// <summary>Total raw message identifiers provided in the request.</summary>
    public int RequestedCount { get; set; }

    /// <summary>Total unique, non-empty message identifiers after normalization.</summary>
    public int UniqueMessageCount { get; set; }

    /// <summary>The normalized unique message identifiers that would be acted on.</summary>
    public List<string> MessageIds { get; set; } = new();

    /// <summary>Requested destination folder identifier or alias when relevant.</summary>
    public string? RequestedDestinationFolderId { get; set; }

    /// <summary>Resolved destination details when the action uses a destination.</summary>
    public MailFolderTargetResolution? Destination { get; set; }

    /// <summary>Desired state value when the action is a state change.</summary>
    public bool? DesiredState { get; set; }

    /// <summary>Expected confirmation token for executing this exact action plan.</summary>
    public string? ConfirmationToken { get; set; }

    /// <summary>Whether a confirmation token was provided while creating the plan.</summary>
    public bool ConfirmationProvided { get; set; }

    /// <summary>Whether the provided confirmation token matched the normalized plan.</summary>
    public bool ConfirmationValidated { get; set; }

    /// <summary>Warnings detected while building the plan.</summary>
    public List<string> Warnings { get; set; } = new();
}
