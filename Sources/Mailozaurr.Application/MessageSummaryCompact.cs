namespace Mailozaurr.Application;

/// <summary>
/// Lightweight projection of a message summary for list and agent scenarios.
/// </summary>
public sealed class MessageSummaryCompact {
    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Provider-specific message identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Folder identifier when known.</summary>
    public string? FolderId { get; set; }

    /// <summary>Message subject.</summary>
    public string? Subject { get; set; }

    /// <summary>Message preview or snippet when available.</summary>
    public string? Preview { get; set; }

    /// <summary>First sender display value when available.</summary>
    public string? From { get; set; }

    /// <summary>When the message was received or observed.</summary>
    public DateTimeOffset? ReceivedAt { get; set; }

    /// <summary>Whether the message is marked read.</summary>
    public bool? IsRead { get; set; }

    /// <summary>Whether the message has attachments.</summary>
    public bool HasAttachments { get; set; }

    /// <summary>Short human-readable summary line.</summary>
    public string Summary { get; set; } = string.Empty;
}