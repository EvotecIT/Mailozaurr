namespace Mailozaurr;

/// <summary>
/// Reusable transform request for cloning a stored action-plan batch into a new environment or mailbox scope.
/// </summary>
public sealed class MessageActionPlanBatchTransformRequest {
    /// <summary>Optional zero-based plan indexes to include from the source batch. When omitted, all plans are included.</summary>
    public List<int> PlanIndexes { get; set; } = new();

    /// <summary>Optional plan names to include from the source batch. When omitted, all names are included.</summary>
    public List<string> PlanNames { get; set; } = new();

    /// <summary>Optional replacement profile identifier for every transformed plan.</summary>
    public string? ProfileId { get; set; }

    /// <summary>Optional replacement mailbox identifier for every transformed plan.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional replacement source folder identifier for every transformed plan.</summary>
    public string? FolderId { get; set; }

    /// <summary>Optional replacement destination folder identifier for move-like plans.</summary>
    public string? DestinationFolderId { get; set; }
}