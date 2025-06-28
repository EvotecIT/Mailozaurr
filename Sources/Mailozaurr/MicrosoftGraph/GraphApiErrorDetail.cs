namespace Mailozaurr;

/// <summary>
/// Detailed information about a Graph API error.
/// </summary>
public class GraphApiErrorDetail {
    /// <summary>
    /// Gets or sets the error code returned by the API.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the human readable error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets additional error details.
    /// </summary>
    [JsonPropertyName("innerError")]
    public GraphApiInnerError InnerError { get; set; }
}
