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

    /// <inheritdoc />
    public override string ToString() => DisplayName ?? base.ToString();
}
