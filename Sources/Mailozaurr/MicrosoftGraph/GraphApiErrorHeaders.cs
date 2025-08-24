using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Mailozaurr;

/// <summary>
/// Represents HTTP headers returned with a Graph API error response.
/// </summary>
public class GraphApiErrorHeaders {
    /// <summary>Gets or sets the Cache-Control header.</summary>
    public string CacheControl { get; set; } = string.Empty;

    /// <summary>Gets or sets the Strict-Transport-Security header.</summary>
    public string StrictTransportSecurity { get; set; } = string.Empty;

    /// <summary>Gets or sets the request-id header.</summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>Gets or sets the client-request-id header.</summary>
    public string ClientRequestId { get; set; } = string.Empty;

    /// <summary>Gets or sets the Date header.</summary>
    public DateTime? Date { get; set; }

    /// <summary>Gets or sets the diagnostic header.</summary>
    public GraphApiDiagnostic? Diagnostic { get; set; }

    /// <summary>Gets or sets additional headers.</summary>
    public Dictionary<string, string> AdditionalHeaders { get; set; } = new();

    /// <summary>Gets or sets additional JSON headers.</summary>
    public Dictionary<string, JsonElement> AdditionalJsonHeaders { get; set; } = new();
}