using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>Simple payload used to send raw MIME data to Gmail.</summary>
public sealed class GmailRawRequest {
    /// <summary>Initializes the request with base64-url encoded MIME content.</summary>
    public GmailRawRequest(string raw) => Raw = raw;

    [JsonPropertyName("raw")]
    /// <summary>Base64-url encoded MIME message.</summary>
    public string Raw { get; }
}

/// <summary>Batch envelope for Microsoft Graph batch API.</summary>
public sealed class GraphBatchPayload {
    [JsonPropertyName("requests")]
    /// <summary>Individual batch requests.</summary>
    public List<GraphBatchRequestPayload> Requests { get; set; } = new();
}

/// <summary>Single request entry inside a Graph batch.</summary>
public sealed class GraphBatchRequestPayload {
    [JsonPropertyName("id")]
    /// <summary>Client-supplied identifier for correlating responses.</summary>
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("method")]
    /// <summary>HTTP method name.</summary>
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    /// <summary>Relative URL within Graph.</summary>
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("headers")]
    /// <summary>Optional per-request headers.</summary>
    public IDictionary<string, string>? Headers { get; set; }
    [JsonPropertyName("body")]
    /// <summary>Optional JSON payload.</summary>
    public JsonElement? Body { get; set; }
}

/// <summary>Batch search request payload for Graph search API.</summary>
public sealed class GraphSearchPayload {
    [JsonPropertyName("requests")]
    /// <summary>Search requests to execute.</summary>
    public List<GraphSearchRequest> Requests { get; set; } = new();
}

/// <summary>Represents a single Graph search request.</summary>
public sealed class GraphSearchRequest {
    [JsonPropertyName("entityTypes")]
    /// <summary>Entity types to search (e.g., message).</summary>
    public string[] EntityTypes { get; set; } = Array.Empty<string>();

    [JsonPropertyName("from")]
    /// <summary>Zero-based result offset.</summary>
    public int From { get; set; }

    [JsonPropertyName("size")]
    /// <summary>Maximum number of hits to return.</summary>
    public int Size { get; set; }

    [JsonPropertyName("query")]
    /// <summary>Query text definition.</summary>
    public GraphSearchQuery Query { get; set; } = new();

    [JsonPropertyName("userScopes")]
    /// <summary>User principals to scope the search to.</summary>
    public string[] UserScopes { get; set; } = Array.Empty<string>();
}

/// <summary>Holds the query text for Graph search.</summary>
public sealed class GraphSearchQuery {
    [JsonPropertyName("queryString")]
    /// <summary>Raw KQL-like query string.</summary>
    public string QueryString { get; set; } = string.Empty;
}

/// <summary>Payload for move/copy operations in Graph.</summary>
public sealed class GraphDestinationRequest {
    [JsonPropertyName("destinationId")]
    /// <summary>Target folder id.</summary>
    public string? DestinationId { get; set; }
}

/// <summary>Payload to mark messages read/unread.</summary>
public sealed class GraphMarkReadRequest {
    [JsonPropertyName("isRead")]
    /// <summary>True to mark read; false to mark unread.</summary>
    public bool IsRead { get; set; }
}

/// <summary>Payload to rename a Graph mail folder.</summary>
public sealed class GraphFolderRenameRequest {
    [JsonPropertyName("displayName")]
    /// <summary>New display name.</summary>
    public string? DisplayName { get; set; }
}

/// <summary>Envelope stored in the pending message log.</summary>
public sealed class PendingMessageLogEnvelope {
    /// <summary>Entry type (upsert or tombstone).</summary>
    public string EntryType { get; set; } = string.Empty;
    /// <summary>Message id affected by this entry.</summary>
    public string? MessageId { get; set; }
    /// <summary>Full pending message record, when applicable.</summary>
    public PendingMessageRecord? Record { get; set; }
}
