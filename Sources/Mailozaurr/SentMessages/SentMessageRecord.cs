namespace Mailozaurr;

/// <summary>
/// Represents a record of a message that has been sent.
/// </summary>
public sealed class SentMessageRecord {
    /// <summary>Identifier of the message.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Comma-separated list of recipients.</summary>
    public string Recipients { get; set; } = string.Empty;

    /// <summary>Subject line of the message.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Time at which the message was sent.</summary>
    public DateTimeOffset Timestamp { get; set; }
}