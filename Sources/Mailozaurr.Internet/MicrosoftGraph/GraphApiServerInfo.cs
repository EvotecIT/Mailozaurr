using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Details about the server handling the request.
/// </summary>
public class GraphApiServerInfo {
    /// <summary>Gets or sets the data center.</summary>
    [JsonPropertyName("DataCenter")]
    public string DataCenter { get; set; } = string.Empty;

    /// <summary>Gets or sets the slice.</summary>
    [JsonPropertyName("Slice")]
    public string Slice { get; set; } = string.Empty;

    /// <summary>Gets or sets the ring.</summary>
    [JsonPropertyName("Ring")]
    public string Ring { get; set; } = string.Empty;

    /// <summary>Gets or sets the scale unit.</summary>
    [JsonPropertyName("ScaleUnit")]
    public string ScaleUnit { get; set; } = string.Empty;

    /// <summary>Gets or sets the role instance.</summary>
    [JsonPropertyName("RoleInstance")]
    public string RoleInstance { get; set; } = string.Empty;
}