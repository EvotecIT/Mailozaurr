namespace Mailozaurr.Hosting;

/// <summary>
/// Represents a persisted reusable batch of normalized message action plans.
/// </summary>
public sealed class MailMessageActionPlanBatch {
    /// <summary>Stable batch identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>User-facing batch name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional operator-facing description.</summary>
    public string? Description { get; set; }

    /// <summary>When the batch was first created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the batch was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>The normalized plans contained in this reusable batch.</summary>
    public List<MessageActionExecutionPlan> Plans { get; set; } = new();
}