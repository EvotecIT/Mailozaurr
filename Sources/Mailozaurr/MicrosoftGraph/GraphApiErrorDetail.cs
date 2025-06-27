namespace Mailozaurr;

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
