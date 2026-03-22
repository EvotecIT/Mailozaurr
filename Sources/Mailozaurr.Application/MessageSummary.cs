namespace Mailozaurr.Application;

/// <summary>
/// Represents normalized summary data for a message returned by search or list operations.
/// </summary>
public sealed class MessageSummary {
    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Provider-specific message identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Provider-specific thread or conversation identifier.</summary>
    public string? ThreadId { get; set; }

    /// <summary>Folder identifier when known.</summary>
    public string? FolderId { get; set; }

    /// <summary>Message subject.</summary>
    public string? Subject { get; set; }

    /// <summary>Message preview or snippet when available.</summary>
    public string? Preview { get; set; }

    /// <summary>Message sender list.</summary>
    public List<MessageRecipient> From { get; set; } = new();

    /// <summary>Primary recipients.</summary>
    public List<MessageRecipient> To { get; set; } = new();

    /// <summary>Carbon-copy recipients.</summary>
    public List<MessageRecipient> Cc { get; set; } = new();

    /// <summary>When the message was sent.</summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>When the message was received or observed.</summary>
    public DateTimeOffset? ReceivedAt { get; set; }

    /// <summary>Whether the message is marked read.</summary>
    public bool? IsRead { get; set; }

    /// <summary>Whether the message has attachments.</summary>
    public bool HasAttachments { get; set; }

    /// <summary>Normalized priority when available.</summary>
    public MessagePriority? Priority { get; set; }
}
