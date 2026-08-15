namespace Mailozaurr;

/// <summary>
/// Lightweight projection of a persisted reusable draft.
/// </summary>
public sealed class MailDraftCompact {
    /// <summary>Stable draft identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>User-facing draft name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Associated profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional subject line.</summary>
    public string? Subject { get; set; }

    /// <summary>Primary recipient count.</summary>
    public int ToCount { get; set; }

    /// <summary>Attachment count.</summary>
    public int AttachmentCount { get; set; }

    /// <summary>When the draft was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Short human-readable summary line.</summary>
    public string Summary { get; set; } = string.Empty;
}