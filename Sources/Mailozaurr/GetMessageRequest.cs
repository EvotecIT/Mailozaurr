namespace Mailozaurr;

/// <summary>
/// Request for retrieving a message.
/// </summary>
public sealed class GetMessageRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional mailbox identifier for multi-mailbox providers.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional folder identifier or folder path.</summary>
    public string? FolderId { get; set; }

    /// <summary>Provider-specific message identifier.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Whether raw content should be included when available.</summary>
    public bool IncludeRawContent { get; set; }
}