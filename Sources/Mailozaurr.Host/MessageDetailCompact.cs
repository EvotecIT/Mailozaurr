namespace Mailozaurr.Hosting;

/// <summary>
/// Lightweight projection of detailed message data for list and agent scenarios.
/// </summary>
public sealed class MessageDetailCompact {
    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Provider-specific message identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Optional normalized summary for the same message.</summary>
    public MessageSummaryCompact? Summary { get; set; }

    /// <summary>Plain-text body preview.</summary>
    public string? TextBodyPreview { get; set; }

    /// <summary>HTML body preview.</summary>
    public string? HtmlBodyPreview { get; set; }

    /// <summary>Attachments associated with the message.</summary>
    public List<AttachmentSummary> Attachments { get; set; } = new();

    /// <summary>Whether raw provider content was available.</summary>
    public bool HasRawContent { get; set; }

    /// <summary>Short human-readable summary line.</summary>
    public string SummaryText { get; set; } = string.Empty;
}