namespace Mailozaurr;

/// <summary>
/// Detailed information about a Graph API error.
/// </summary>
/// <remarks>
/// Included when the Graph service returns structured error information
/// that can assist with troubleshooting.
/// </remarks>
public class GraphApiErrorDetail {
    /// <summary>
    /// Gets or sets the error code returned by the API.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human readable error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional error details.
    /// </summary>
    [JsonPropertyName("innerError")]
    public GraphApiInnerError InnerError { get; set; } = new();
}
