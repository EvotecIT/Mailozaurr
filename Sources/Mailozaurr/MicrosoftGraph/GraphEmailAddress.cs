namespace Mailozaurr;

/// <summary>
/// Represents an email address object for Graph API payloads.
/// </summary>
public class GraphEmailAddress {
    [JsonPropertyName("emailAddress")]
    public GraphEmail Email { get; set; }
}
