using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Authorization information returned by Microsoft Graph OAuth flows.
/// </summary>
public class GraphAuthorization {
    /// <summary>The type of token issued.</summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }

    /// <summary>The access token value.</summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    /// <summary>Time when the access token expires.</summary>
    [JsonPropertyName("expires_on")]
    public DateTimeOffset ExpiresOn { get; set; }
}