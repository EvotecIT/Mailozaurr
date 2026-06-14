using System.Collections.Generic;
using System.Text.Json;
namespace Mailozaurr;

/// <summary>
/// Represents the response for a single request within a Microsoft Graph batch.
/// </summary>
public class GraphBatchResult {
    /// <summary>Identifier matching the originating request.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>HTTP status code returned by the request.</summary>
    public int Status { get; set; }
    /// <summary>Response headers.</summary>
    public IDictionary<string, string>? Headers { get; set; }
    /// <summary>Raw JSON body of the response.</summary>
    public JsonElement? Body { get; set; }
}