namespace Mailozaurr;

/// <summary>
/// Statistics for a single mail folder.
/// </summary>
/// <remarks>
/// Retrieved via the <c>/mailFolders/{id}/messages</c> endpoints
/// to provide quick insights into mailbox usage.
/// </remarks>
public class GraphMailboxFolderStatistics {
    /// <summary>Folder identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name of the folder.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional well-known name such as 'inbox' or 'sentitems'.</summary>
    public string? WellKnownName { get; set; }

    /// <summary>Total number of items in this folder.</summary>
    public int TotalItemCount { get; set; }

    /// <summary>Number of unread items in this folder.</summary>
    public int UnreadItemCount { get; set; }

    /// <summary>Number of subfolders contained within this folder.</summary>
    public int ChildFolderCount { get; set; }
}
