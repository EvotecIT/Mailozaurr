namespace Mailozaurr.Application;

/// <summary>
/// Per-message outcome for a bulk message action.
/// </summary>
public sealed class MessageActionItemResult {
    /// <summary>Provider-specific message identifier.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Whether the action succeeded for this message.</summary>
    public bool Succeeded { get; set; }

    /// <summary>Optional normalized status/error code.</summary>
    public string? Code { get; set; }

    /// <summary>Optional human-readable action outcome.</summary>
    public string? Message { get; set; }
}
