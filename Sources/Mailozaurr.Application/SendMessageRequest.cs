namespace Mailozaurr.Application;

/// <summary>
/// Request for sending or queueing a message.
/// </summary>
public sealed class SendMessageRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Draft to send or queue.</summary>
    public DraftMessage Message { get; set; } = new();

    /// <summary>Whether a failed immediate send should be persisted for retry.</summary>
    public bool QueueOnFailure { get; set; }

    /// <summary>Optional scheduled send time for queue-capable implementations.</summary>
    public DateTimeOffset? NotBefore { get; set; }
}
