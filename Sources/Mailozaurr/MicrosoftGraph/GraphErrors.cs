namespace Mailozaurr;

/// <summary>
/// Container for an error returned by the Graph API.
/// </summary>
public class GraphApiError {
    /// <summary>
    /// Gets or sets the error details returned by the API.
    /// </summary>
    [JsonPropertyName("error")]
    public GraphApiErrorDetail Error { get; set; }
}
