namespace Mailozaurr;

/// <summary>
/// Request for retrieving multiple messages.
/// </summary>
public sealed class GetMessagesRequest {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional mailbox identifier for multi-mailbox providers.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional folder identifier or folder path.</summary>
    public string? FolderId { get; set; }

    /// <summary>Provider-specific message identifiers.</summary>
    public List<string> MessageIds { get; set; } = new();

    /// <summary>Whether raw content should be included when available.</summary>
    public bool IncludeRawContent { get; set; }
}