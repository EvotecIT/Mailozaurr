using System.Text.Json.Serialization;
namespace Mailozaurr;

/// <summary>
/// Represents a single internet message header returned by Microsoft Graph.
/// </summary>
/// <remarks>
/// Only name and value are exposed as these are typically the
/// only fields required when processing headers.
/// </remarks>
public class GraphInternetMessageHeader {
    /// <summary>Header name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>Header value.</summary>
    [JsonPropertyName("value")]
    public string Value { get; set; }
}

