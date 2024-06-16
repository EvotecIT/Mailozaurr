namespace Mailozaurr;

public class GraphUploadSessionResult {
    [JsonPropertyName("uploadUrl")]
    public string UploadUrl { get; set; }
}

public class GraphAuthorization {
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }
}