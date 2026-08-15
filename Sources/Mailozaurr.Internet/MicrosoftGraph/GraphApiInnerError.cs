namespace Mailozaurr;

/// <summary>
/// Additional error details returned by the Graph API.
/// </summary>
/// <remarks>
/// This object is nested inside <see cref="GraphApiErrorDetail"/> when
/// the service includes extra context about a failure.
/// </remarks>
public class GraphApiInnerError {
    /// <summary>
    /// Gets or sets the request identifier associated with the error.
    /// </summary>
    [JsonPropertyName("request-id")]
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client request identifier associated with the error.
    /// </summary>
    [JsonPropertyName("client-request-id")]
    public string ClientRequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp of when the error occurred.
    /// </summary>
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}