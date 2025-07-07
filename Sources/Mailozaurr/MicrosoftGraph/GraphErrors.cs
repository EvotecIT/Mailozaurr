namespace Mailozaurr;

/// <summary>
/// Container for an error returned by the Graph API.
/// </summary>
public class GraphApiError {
    [JsonPropertyName("error")]
    /// <summary>
    /// Gets or sets the error details returned by the API.
    /// </summary>
    public GraphApiErrorDetail Error { get; set; }
}