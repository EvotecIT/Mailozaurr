using System.Collections.Generic;
namespace Mailozaurr;

/// <summary>
/// Represents a single request within a Microsoft Graph batch operation.
/// </summary>
public class GraphBatchRequest {
    /// <summary>Unique request identifier.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>HTTP method to execute.</summary>
    public GraphHttpMethod Method { get; set; }
    /// <summary>Relative request URL (e.g. <c>/me/messages</c>).</summary>
    public string Url { get; set; } = string.Empty;
    /// <summary>Optional headers to include with the request.</summary>
    public IDictionary<string, string>? Headers { get; set; }
    /// <summary>Optional JSON body for POST/PUT/PATCH requests.</summary>
    public object? Body { get; set; }
}
