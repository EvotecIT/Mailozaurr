namespace Mailozaurr;

/// <summary>
/// Aggregated mailbox statistics retrieved from Microsoft Graph.
/// </summary>
/// <remarks>
/// Useful for reporting or monitoring mailbox usage trends over time.
/// </remarks>
public class GraphMailboxStatistics {
    /// <summary>User principal name of the mailbox.</summary>
    public string UserPrincipalName { get; set; }

    /// <summary>Total number of messages across all folders.</summary>
    public int MessageCount { get; set; }

    /// <summary>Total number of messages that contain attachments.</summary>
    public int MessagesWithAttachments { get; set; }

    /// <summary>Total size of all attachments in bytes.</summary>
    public long TotalAttachmentSize { get; set; }

    /// <summary>Total number of folders in the mailbox.</summary>
    public int TotalFolders { get; set; }

    /// <summary>Statistics for individual folders.</summary>
    public List<GraphMailboxFolderStatistics> FolderStatistics { get; } = new();
}
