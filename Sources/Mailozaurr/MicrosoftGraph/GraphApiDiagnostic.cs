using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Diagnostic information returned by the Graph API.
/// </summary>
public class GraphApiDiagnostic {
    /// <summary>Gets or sets server information details.</summary>
    [JsonPropertyName("ServerInfo")]
    public GraphApiServerInfo ServerInfo { get; set; } = new();
}