namespace Mailozaurr;

/// <summary>
/// Represents a message returned by Gmail REST API.
/// </summary>
public sealed class GmailMessage {
    /// <summary>Unique message identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Thread identifier.</summary>
    public string? ThreadId { get; set; }

    /// <summary>Message snippet.</summary>
    public string? Snippet { get; set; }

    /// <summary>Raw MIME content encoded as base64url.</summary>
    public string? Raw { get; set; }
}
