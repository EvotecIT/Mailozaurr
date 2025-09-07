namespace Mailozaurr;

/// <summary>
/// Represents a message pending to be sent.
/// </summary>
public sealed class PendingMessageRecord {
    /// <summary>Identifier of the message.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Time at which the message was queued.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Time when the next send attempt should occur.</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>Base64-encoded MIME message.</summary>
    public string MimeMessage { get; set; } = string.Empty;

    /// <summary>SMTP server used when the message was queued.</summary>
    public string? Server { get; set; }

    /// <summary>Port of the SMTP server.</summary>
    public int? Port { get; set; }

    /// <summary>User name for authentication.</summary>
    public string? UserName { get; set; }

    /// <summary>Password for authentication.</summary>
    public string? Password { get; set; }
}
