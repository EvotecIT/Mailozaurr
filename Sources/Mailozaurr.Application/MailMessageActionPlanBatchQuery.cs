namespace Mailozaurr.Application;

/// <summary>
/// Optional filters for persisted action-plan batch queries.
/// </summary>
public sealed class MailMessageActionPlanBatchQuery {
    /// <summary>Optional human-readable plan names that must exist in the returned batch.</summary>
    public List<string> PlanNames { get; set; } = new();

    /// <summary>Optional profile identifiers that must be referenced by the returned batch.</summary>
    public List<string> ProfileIds { get; set; } = new();

    /// <summary>Optional normalized action names that must be referenced by the returned batch.</summary>
    public List<string> Actions { get; set; } = new();

    /// <summary>Optional sort order for the returned batches.</summary>
    public MailMessageActionPlanBatchSortBy SortBy { get; set; } = MailMessageActionPlanBatchSortBy.Id;

    /// <summary>When true, reverses the selected sort order.</summary>
    public bool Descending { get; set; }
}