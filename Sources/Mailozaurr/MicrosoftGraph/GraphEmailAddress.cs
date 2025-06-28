namespace Mailozaurr;

/// <summary>
/// Represents an email address object for Graph API payloads.
/// </summary>
public class GraphEmailAddress {
    /// <summary>
    /// Gets or sets the email details.
    /// </summary>
    [JsonPropertyName("emailAddress")]
    public GraphEmail Email { get; set; }
}
