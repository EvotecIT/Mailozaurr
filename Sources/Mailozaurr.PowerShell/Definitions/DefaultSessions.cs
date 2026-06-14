namespace Mailozaurr.PowerShell;

using System.Threading;

/// <summary>
/// Provides access to the last successful connection objects used by the module.
/// These are automatically updated by Connect-EmailGraph, Connect-IMAP and
/// Connect-POP3 and consumed by other cmdlets when a connection is not
/// explicitly provided.
/// </summary>
public static class DefaultSessions {
    private static readonly AsyncLocal<GraphConnectionInfo?> _graphSession = new();
    private static readonly AsyncLocal<ImapConnectionInfo?> _imapSession = new();
    private static readonly AsyncLocal<PopConnectionInfo?> _pop3Session = new();

    /// <summary>Last Microsoft Graph connection.</summary>
    public static GraphConnectionInfo? GraphSession {
        get => _graphSession.Value;
        set => _graphSession.Value = value;
    }

    /// <summary>Last IMAP connection.</summary>
    public static ImapConnectionInfo? ImapSession {
        get => _imapSession.Value;
        set => _imapSession.Value = value;
    }

    /// <summary>Last POP3 connection.</summary>
    public static PopConnectionInfo? Pop3Session {
        get => _pop3Session.Value;
        set => _pop3Session.Value = value;
    }
}