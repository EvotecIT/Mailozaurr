namespace Mailozaurr;

/// <summary>
/// Represents a message pending to be sent.
/// </summary>
public sealed class PendingMessageRecord {
    /// <summary>Identifier of the message.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Time at which the message was queued.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Base64-encoded MIME message.</summary>
    public string MimeMessage { get; set; } = string.Empty;
}
