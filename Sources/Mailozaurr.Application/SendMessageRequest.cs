namespace Mailozaurr.Application;

/// <summary>
/// Request for sending or queueing a message.
/// </summary>
public sealed class SendMessageRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Draft to send or queue.</summary>
    public DraftMessage Message { get; set; } = new();

    /// <summary>Whether queueing should be preferred when supported.</summary>
    public bool PreferQueue { get; set; } = true;

    /// <summary>Whether sending immediately is required.</summary>
    public bool RequireImmediateSend { get; set; }

    /// <summary>Optional scheduled send time for queue-capable implementations.</summary>
    public DateTimeOffset? NotBefore { get; set; }
}
