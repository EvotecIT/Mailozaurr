namespace Mailozaurr;

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
