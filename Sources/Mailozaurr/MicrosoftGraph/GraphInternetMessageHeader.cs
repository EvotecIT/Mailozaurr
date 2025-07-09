using System.Text.Json.Serialization;
namespace Mailozaurr;

/// <summary>
/// Represents a single internet message header returned by Microsoft Graph.
/// </summary>
public class GraphInternetMessageHeader {
    /// <summary>Header name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>Header value.</summary>
    [JsonPropertyName("value")]
    public string Value { get; set; }
}

