using System.Collections.Generic;
using System.Threading;

namespace Mailozaurr;

/// <summary>
/// Represents a message pending to be sent.
/// </summary>
public sealed class PendingMessageRecord {
    private int attemptCount;

    /// <summary>Identifier of the message.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Time at which the message was queued.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Time when the next send attempt should occur.</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>Number of times delivery has been attempted.</summary>
    public int AttemptCount {
        get => Volatile.Read(ref attemptCount);
        set => Volatile.Write(ref attemptCount, value);
    }

    /// <summary>Base64-encoded MIME message.</summary>
    public string MimeMessage { get; set; } = string.Empty;

    /// <summary>SMTP server used when the message was queued.</summary>
    public string? Server { get; set; }

    /// <summary>Port of the SMTP server.</summary>
    public int? Port { get; set; }

    /// <summary>User name for authentication.</summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Protected password encoded as Base64.
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

    /// <summary>
    /// Atomically increments <see cref="AttemptCount"/> and returns the updated value.
    /// </summary>
    public int IncrementAttemptCount() => Interlocked.Increment(ref attemptCount);

    /// <summary>
    /// Atomically sets <see cref="AttemptCount"/> to the specified value.
    /// </summary>
    /// <param name="value">Value assigned to the attempt counter.</param>
    /// <returns>The previous value stored in the attempt counter.</returns>
    public int ExchangeAttemptCount(int value) => Interlocked.Exchange(ref attemptCount, value);

    /// <summary>
    /// Creates a detached copy of this record.
    /// </summary>
    public PendingMessageRecord Clone() => new() {
        MessageId = MessageId,
        Timestamp = Timestamp,
        NextAttemptAt = NextAttemptAt,
        AttemptCount = AttemptCount,
        MimeMessage = MimeMessage,
        Server = Server,
        Port = Port,
        UserName = UserName,
        Password = Password,
        Provider = Provider,
        ProviderData = new Dictionary<string, string>(ProviderData)
    };
}