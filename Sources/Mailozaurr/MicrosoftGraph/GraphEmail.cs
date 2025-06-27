namespace Mailozaurr;

/// <summary>
/// Simple email address container.
/// </summary>
public class GraphEmail {
    [JsonPropertyName("address")]
    public string Address { get; set; }
}
