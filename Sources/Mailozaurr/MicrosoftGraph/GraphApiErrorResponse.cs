using System.Net;

namespace Mailozaurr;

/// <summary>
/// Parsed details of a Graph API error message.
/// </summary>
public class GraphApiErrorResponse {
    /// <summary>Gets or sets the HTTP method.</summary>
    public GraphHttpMethod? Method { get; set; }

    /// <summary>Gets or sets the request URI.</summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>Gets or sets the status code.</summary>
    public HttpStatusCode StatusCode { get; set; }

    /// <summary>Gets or sets the headers.</summary>
    public GraphApiErrorHeaders Headers { get; set; } = new();

    /// <summary>Gets or sets the error details.</summary>
    public GraphApiErrorDetail? Error { get; set; }
}