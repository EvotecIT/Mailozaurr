namespace Mailozaurr.Application;

/// <summary>
/// Lightweight projection of a persisted reusable message action plan batch.
/// </summary>
public sealed class MailMessageActionPlanBatchCompact {
    /// <summary>Stable batch identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>User-facing batch name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Total plans in the batch.</summary>
    public int PlanCount { get; set; }

    /// <summary>Total plans currently ready for execution.</summary>
    public int ReadyPlanCount { get; set; }

    /// <summary>Total distinct profiles referenced by the stored plans.</summary>
    public int ProfileCount { get; set; }

    /// <summary>Lightweight human-readable plan names available in the batch.</summary>
    public List<string> PlanNames { get; set; } = new();

    /// <summary>Last updated timestamp for the batch.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Human-readable summary.</summary>
    public string Summary { get; set; } = string.Empty;
}
