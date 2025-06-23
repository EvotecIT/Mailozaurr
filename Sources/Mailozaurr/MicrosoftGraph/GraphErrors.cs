namespace Mailozaurr;

/// <summary>
/// Container for an error returned by the Graph API.
/// </summary>
public class GraphApiError {
    [JsonPropertyName("error")]
    public GraphApiErrorDetail Error { get; set; }
}

/// <summary>
/// Detailed information about a Graph API error.
/// </summary>
public class GraphApiErrorDetail {
    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("innerError")]
    public GraphApiInnerError InnerError { get; set; }
}

/// <summary>
/// Additional error details returned by the Graph API.
/// </summary>
public class GraphApiInnerError {
    [JsonPropertyName("request-id")]
    public string RequestId { get; set; }

    [JsonPropertyName("client-request-id")]
    public string ClientRequestId { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}