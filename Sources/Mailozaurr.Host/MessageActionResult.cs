namespace Mailozaurr.Hosting;

/// <summary>
/// Aggregate outcome for a bulk message action.
/// </summary>
public sealed class MessageActionResult : OperationResult {
    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Total unique message ids requested.</summary>
    public int RequestedCount { get; set; }

    /// <summary>Total messages whose action succeeded.</summary>
    public int SucceededCount { get; set; }

    /// <summary>Total messages whose action failed.</summary>
    public int FailedCount { get; set; }

    /// <summary>Per-message outcomes in request order.</summary>
    public List<MessageActionItemResult> Results { get; set; } = new();
}