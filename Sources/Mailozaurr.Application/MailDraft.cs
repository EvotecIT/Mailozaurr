namespace Mailozaurr.Application;

/// <summary>
/// Represents a persisted reusable draft.
/// </summary>
public sealed class MailDraft {
    /// <summary>Stable draft identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>User-facing draft name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When the draft was first created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the draft was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Normalized draft message payload.</summary>
    public DraftMessage Message { get; set; } = new();
}