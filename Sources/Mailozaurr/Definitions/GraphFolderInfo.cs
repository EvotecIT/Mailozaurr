namespace Mailozaurr;

/// <summary>
/// Provides a convenient view over a Graph mail folder.
/// </summary>
public class GraphFolderInfo {
    /// <summary>Creates a new instance from a dictionary returned by Graph.</summary>
    public GraphFolderInfo(System.Collections.Generic.Dictionary<string, object> raw, string? userPrincipalName = null) {
        Raw = raw;
        UserPrincipalName = userPrincipalName;
        if (raw.TryGetValue("id", out var idObj)) Id = idObj as string;
        if (raw.TryGetValue("displayName", out var dnObj)) DisplayName = dnObj as string;
        if (raw.TryGetValue("parentFolderId", out var parentObj)) ParentFolderId = parentObj as string;
        if (raw.TryGetValue("childFolderCount", out var cfcObj) && int.TryParse(cfcObj.ToString(), out var cfc)) ChildFolderCount = cfc;
        if (raw.TryGetValue("unreadItemCount", out var unreadObj) && int.TryParse(unreadObj.ToString(), out var unread)) UnreadItemCount = unread;
        if (raw.TryGetValue("totalItemCount", out var totalObj) && int.TryParse(totalObj.ToString(), out var total)) TotalItemCount = total;
        if (raw.TryGetValue("isHidden", out var hiddenObj) && bool.TryParse(hiddenObj.ToString(), out var hidden)) IsHidden = hidden;
        if (raw.TryGetValue("wellKnownName", out var knownObj)) WellKnownName = knownObj as string;
    }

    /// <summary>The original dictionary.</summary>
    public System.Collections.Generic.Dictionary<string, object> Raw { get; }

    /// <summary>User principal name of the mailbox.</summary>
    public string? UserPrincipalName { get; set; }

    /// <summary>Folder identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Display name.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Identifier of the parent folder.</summary>
    public string? ParentFolderId { get; set; }

    /// <summary>Number of child folders.</summary>
    public int? ChildFolderCount { get; set; }

    /// <summary>Number of unread items in the folder.</summary>
    public int? UnreadItemCount { get; set; }

    /// <summary>Total number of items in the folder.</summary>
    public int? TotalItemCount { get; set; }

    /// <summary>Indicates whether the folder is hidden.</summary>
    public bool? IsHidden { get; set; }

    /// <summary>Well-known name of the folder.</summary>
    public string? WellKnownName { get; set; }

    /// <summary>Full folder path relative to the mailbox root.</summary>
    public string? FullPath { get; set; }

    /// <inheritdoc />
    public override string ToString() => DisplayName ?? base.ToString();
}
