using System.Collections.Generic;

namespace Mailozaurr;

/// <summary>
/// Represents a single page of results returned by Microsoft Graph.
/// </summary>
public class GraphPage<T> {
    /// <summary>Items returned in the current page.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>Link to the next page (when present).</summary>
    public string? NextLink { get; }

    /// <summary>Creates a page instance.</summary>
    public GraphPage(IReadOnlyList<T> items, string? nextLink) {
        Items = items ?? new List<T>();
        if (nextLink == null) {
            NextLink = null;
        } else {
            var trimmed = nextLink.Trim();
            NextLink = trimmed.Length == 0 ? null : trimmed;
        }
    }
}

/// <summary>
/// Represents a delta page returned by Graph delta endpoints.
/// </summary>
public sealed class GraphDeltaPage<T> : GraphPage<T> {
    /// <summary>Link to the delta cursor (when returned by Graph).</summary>
    public string? DeltaLink { get; }

    /// <summary>Identifiers of deleted items (tombstones) returned in the current page.</summary>
    public IReadOnlyList<string> DeletedIds { get; }

    /// <summary>The recommended cursor to persist (nextLink when present, otherwise deltaLink).</summary>
    public string? Cursor => NextLink ?? DeltaLink;

    /// <summary>Creates a delta page instance.</summary>
    public GraphDeltaPage(IReadOnlyList<T> items, string? nextLink, string? deltaLink, IReadOnlyList<string> deletedIds)
        : base(items, nextLink) {
        if (deltaLink == null) {
            DeltaLink = null;
        } else {
            var trimmed = deltaLink.Trim();
            DeltaLink = trimmed.Length == 0 ? null : trimmed;
        }
        DeletedIds = deletedIds ?? new List<string>();
    }
}
