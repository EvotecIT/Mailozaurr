namespace Mailozaurr;

public sealed class SentMessageRecord {
    public string MessageId { get; set; } = string.Empty;
    public string Recipients { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}
