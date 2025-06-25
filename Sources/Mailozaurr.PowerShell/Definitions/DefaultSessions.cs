namespace Mailozaurr.PowerShell;

/// <summary>
/// Provides access to the last successful connection objects used by the module.
/// These are automatically updated by Connect-EmailGraph, Connect-IMAP and
/// Connect-POP3 and consumed by other cmdlets when a connection is not
/// explicitly provided.
/// </summary>
public static class DefaultSessions {
    /// <summary>Last Microsoft Graph connection.</summary>
    public static GraphConnectionInfo? GraphSession { get; set; }

    /// <summary>Last IMAP connection.</summary>
    public static ImapConnectionInfo? ImapSession { get; set; }

    /// <summary>Last POP3 connection.</summary>
    public static PopConnectionInfo? Pop3Session { get; set; }
}
