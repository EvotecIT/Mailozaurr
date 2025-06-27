namespace Mailozaurr;

/// <summary>
/// Container for an error returned by the Graph API.
/// </summary>
public class GraphApiError {
    [JsonPropertyName("error")]
    public GraphApiErrorDetail Error { get; set; }
}