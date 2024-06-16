namespace Mailozaurr;

public class GraphApiError {
    [JsonPropertyName("error")]
    public GraphApiErrorDetail Error { get; set; }
}

public class GraphApiErrorDetail {
    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("innerError")]
    public GraphApiInnerError InnerError { get; set; }
}

public class GraphApiInnerError {
    [JsonPropertyName("request-id")]
    public string RequestId { get; set; }

    [JsonPropertyName("client-request-id")]
    public string ClientRequestId { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}