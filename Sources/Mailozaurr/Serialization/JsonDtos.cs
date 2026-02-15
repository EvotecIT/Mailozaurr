using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>Simple payload used to send raw MIME data to Gmail.</summary>
public sealed class GmailRawRequest {
    /// <summary>Initializes the request with base64-url encoded MIME content.</summary>
    public GmailRawRequest(string raw) => Raw = raw;

    /// <summary>Base64-url encoded MIME message.</summary>
    [JsonPropertyName("raw")]
    public string Raw { get; }
}

/// <summary>Batch envelope for Microsoft Graph batch API.</summary>
public sealed class GraphBatchPayload {
    /// <summary>Individual batch requests.</summary>
    [JsonPropertyName("requests")]
    public List<GraphBatchRequestPayload> Requests { get; set; } = new();
}

/// <summary>Single request entry inside a Graph batch.</summary>
public sealed class GraphBatchRequestPayload {
    /// <summary>Client-supplied identifier for correlating responses.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>HTTP method name.</summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>Relative URL within Graph.</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>Optional per-request headers.</summary>
    [JsonPropertyName("headers")]
    public IDictionary<string, string>? Headers { get; set; }

    /// <summary>Optional JSON payload.</summary>
    [JsonPropertyName("body")]
    public JsonElement? Body { get; set; }
}

/// <summary>Batch search request payload for Graph search API.</summary>
public sealed class GraphSearchPayload {
    /// <summary>Search requests to execute.</summary>
    [JsonPropertyName("requests")]
    public List<GraphSearchRequest> Requests { get; set; } = new();
}

/// <summary>Represents a single Graph search request.</summary>
public sealed class GraphSearchRequest {
    /// <summary>Entity types to search (e.g., message).</summary>
    [JsonPropertyName("entityTypes")]
    public string[] EntityTypes { get; set; } = Array.Empty<string>();

    /// <summary>Zero-based result offset.</summary>
    [JsonPropertyName("from")]
    public int From { get; set; }

    /// <summary>Maximum number of hits to return.</summary>
    [JsonPropertyName("size")]
    public int Size { get; set; }

    /// <summary>Query text definition.</summary>
    [JsonPropertyName("query")]
    public GraphSearchQuery Query { get; set; } = new();

    /// <summary>User principals to scope the search to.</summary>
    [JsonPropertyName("userScopes")]
    public string[] UserScopes { get; set; } = Array.Empty<string>();
}

/// <summary>Holds the query text for Graph search.</summary>
public sealed class GraphSearchQuery {
    /// <summary>Raw KQL-like query string.</summary>
    [JsonPropertyName("queryString")]
    public string QueryString { get; set; } = string.Empty;
}

/// <summary>Payload for move/copy operations in Graph.</summary>
public sealed class GraphDestinationRequest {
    /// <summary>Target folder id.</summary>
    [JsonPropertyName("destinationId")]
    public string? DestinationId { get; set; }
}

/// <summary>Payload to mark messages read/unread.</summary>
public sealed class GraphMarkReadRequest {
    /// <summary>True to mark read; false to mark unread.</summary>
    [JsonPropertyName("isRead")]
    public bool IsRead { get; set; }
}

/// <summary>Payload to flag/unflag messages.</summary>
public sealed class GraphSetFlagRequest {
    /// <summary>Flag object.</summary>
    [JsonPropertyName("flag")]
    public GraphSetFlagRequestFlag Flag { get; set; } = new();
}

/// <summary>Flag object for <see cref="GraphSetFlagRequest"/>.</summary>
public sealed class GraphSetFlagRequestFlag {
    /// <summary>Flag status (for example: flagged, notFlagged).</summary>
    [JsonPropertyName("flagStatus")]
    public string? FlagStatus { get; set; }
}

/// <summary>Payload to rename a Graph mail folder.</summary>
public sealed class GraphFolderRenameRequest {
    /// <summary>New display name.</summary>
    [JsonPropertyName("displayName")]
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
