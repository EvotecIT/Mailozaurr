using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Represents a Microsoft Graph mail folder as returned by the <c>/mailFolders</c> endpoints.
/// </summary>
public sealed class GraphMailFolder {
    /// <summary>Folder identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Folder display name.</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Parent folder identifier (may be null for top-level folders).</summary>
    [JsonPropertyName("parentFolderId")]
    public string? ParentFolderId { get; set; }

    /// <summary>Number of child folders.</summary>
    [JsonPropertyName("childFolderCount")]
    public int? ChildFolderCount { get; set; }

    /// <summary>Well-known folder name (for example, <c>inbox</c> or <c>sentitems</c>).</summary>
    [JsonPropertyName("wellKnownName")]
    public string? WellKnownName { get; set; }

    /// <summary>Total number of items in the folder.</summary>
    [JsonPropertyName("totalItemCount")]
    public int? TotalItemCount { get; set; }

    /// <summary>Number of unread items in the folder.</summary>
    [JsonPropertyName("unreadItemCount")]
    public int? UnreadItemCount { get; set; }
}