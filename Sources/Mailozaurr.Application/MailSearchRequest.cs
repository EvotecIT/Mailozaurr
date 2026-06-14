namespace Mailozaurr.Application;

/// <summary>
/// Request for searching or listing messages.
/// </summary>
public sealed class MailSearchRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional mailbox identifier.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional folder identifier.</summary>
    public string? FolderId { get; set; }

    /// <summary>Optional free-text query.</summary>
    public string? QueryText { get; set; }

    /// <summary>Optional subject filter.</summary>
    public string? SubjectContains { get; set; }

    /// <summary>Optional sender filter.</summary>
    public string? FromContains { get; set; }

    /// <summary>Optional recipient filter.</summary>
    public string? ToContains { get; set; }

    /// <summary>Only include messages with attachments.</summary>
    public bool HasAttachments { get; set; }

    /// <summary>Only include unread messages when set to <c>true</c>.</summary>
    public bool? IsRead { get; set; }

    /// <summary>Optional lower date bound.</summary>
    public DateTimeOffset? Since { get; set; }

    /// <summary>Optional upper date bound.</summary>
    public DateTimeOffset? Before { get; set; }

    /// <summary>Maximum number of results to return.</summary>
    public int? Limit { get; set; }
}