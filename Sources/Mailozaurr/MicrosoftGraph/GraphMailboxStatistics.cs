namespace Mailozaurr;

/// <summary>
/// Aggregated mailbox statistics retrieved from Microsoft Graph.
/// </summary>
public class GraphMailboxStatistics {
    /// <summary>User principal name of the mailbox.</summary>
    public string UserPrincipalName { get; set; }

    /// <summary>Total number of messages in the mailbox.</summary>
    public int MessageCount { get; set; }

    /// <summary>Total size of all attachments in bytes.</summary>
    public long TotalAttachmentSize { get; set; }
}
