namespace Mailozaurr;

/// <summary>
/// Simple email address container.
/// </summary>
public class GraphEmail {
    /// <summary>
    /// Gets or sets the email address value.
    /// </summary>
    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;
}
