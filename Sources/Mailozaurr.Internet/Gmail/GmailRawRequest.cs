using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>Payload used to send raw MIME data to Gmail.</summary>
public sealed class GmailRawRequest {
    /// <summary>Initializes the request with base64-url encoded MIME content.</summary>
    public GmailRawRequest(string raw) => Raw = raw;

    /// <summary>Base64-url encoded MIME message.</summary>
    [JsonPropertyName("raw")]
    public string Raw { get; }
}
