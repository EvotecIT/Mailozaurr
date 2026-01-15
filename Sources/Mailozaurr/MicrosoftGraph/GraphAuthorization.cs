using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Authorization information returned by Microsoft Graph OAuth flows.
/// </summary>
/// <remarks>
/// This object is serialized from the JSON response after acquiring
/// an access token using device code or interactive login.
/// </remarks>
public class GraphAuthorization {
    /// <summary>The type of token issued.</summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    /// <summary>The access token value.</summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Time when the access token expires.</summary>
    [JsonPropertyName("expires_on")]
    [JsonConverter(typeof(UnixTimeSecondsDateTimeOffsetConverter))]
    public DateTimeOffset ExpiresOn { get; set; }
}
