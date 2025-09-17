using System.Collections.Generic;

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

    /// <summary>Number of times delivery has been attempted.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Base64-encoded MIME message.</summary>
    public string MimeMessage { get; set; } = string.Empty;

    /// <summary>SMTP server used when the message was queued.</summary>
    public string? Server { get; set; }

    /// <summary>Port of the SMTP server.</summary>
    public int? Port { get; set; }

    /// <summary>User name for authentication.</summary>
    public string? UserName { get; set; }

    /// <summary>
    /// DPAPI protected password encoded as Base64.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Provider that should handle the queued message.
    /// </summary>
    public EmailProvider Provider { get; set; } = EmailProvider.None;

    private Dictionary<string, string>? providerData;

    /// <summary>
    /// Arbitrary provider-specific fields required to resume delivery.
    /// </summary>
    public Dictionary<string, string> ProviderData {
        get => providerData ??= new Dictionary<string, string>();
        set => providerData = value ?? new Dictionary<string, string>();
    }
}
