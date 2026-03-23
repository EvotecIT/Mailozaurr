namespace Mailozaurr.Application;

/// <summary>
/// Rich lightweight summary of a persisted reusable message action plan batch.
/// </summary>
public sealed class MailMessageActionPlanBatchSummary {
    /// <summary>Stable batch identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>User-facing batch name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Total plans in the batch.</summary>
    public int PlanCount { get; set; }

    /// <summary>Total plans currently ready for execution.</summary>
    public int ReadyPlanCount { get; set; }

    /// <summary>Distinct profile identifiers referenced by the stored plans.</summary>
    public List<string> ProfileIds { get; set; } = new();

    /// <summary>Per-action plan counts keyed by normalized action name.</summary>
    public Dictionary<string, int> ActionCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Lightweight human-readable plan names available in the batch.</summary>
    public List<string> PlanNames { get; set; } = new();

    /// <summary>Last updated timestamp for the batch.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Human-readable summary.</summary>
    public string Summary { get; set; } = string.Empty;
}
