namespace Mailozaurr;

/// <summary>
/// Represents normalized detailed message data.
/// </summary>
public sealed class MessageDetail {
    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Provider-specific message identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Optional normalized summary for the same message.</summary>
    public MessageSummary? Summary { get; set; }

    /// <summary>Plain text body.</summary>
    public string? TextBody { get; set; }

    /// <summary>HTML body.</summary>
    public string? HtmlBody { get; set; }

    /// <summary>Attachments associated with the message.</summary>
    public List<AttachmentSummary> Attachments { get; set; } = new();

    /// <summary>Optional raw MIME or provider-native payload.</summary>
    public string? RawContent { get; set; }
}