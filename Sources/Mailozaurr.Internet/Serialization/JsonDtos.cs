namespace Mailozaurr;

/// <summary>Envelope stored in the pending message log.</summary>
public sealed class PendingMessageLogEnvelope {
    /// <summary>Entry type (upsert or tombstone).</summary>
    public string EntryType { get; set; } = string.Empty;

    /// <summary>Message id affected by this entry.</summary>
    public string? MessageId { get; set; }

    /// <summary>Full pending message record, when applicable.</summary>
    public PendingMessageRecord? Record { get; set; }
}
