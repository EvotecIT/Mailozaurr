using System;
using System.Collections.Generic;
using System.IO;
using MailKit;
using MailKit.Net.Imap;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Represents the result of a successful IMAP connection, including the client and connection details.
/// </summary>
public class ImapConnectionInfo : ConnectionInfoBase {
    /// <summary>IMAP server URI.</summary>
    public string Uri { get; set; } = string.Empty;
    /// <summary>Authentication mechanisms supported by the server.</summary>
    public ISet<string>? AuthenticationMechanisms { get; set; }
    /// <summary>Server capabilities.</summary>
    public ImapCapabilities Capabilities { get; set; }
    /// <summary>The underlying stream.</summary>
    public Stream? Stream { get; set; }
    /// <summary>Current state of the connection.</summary>
    public object? State { get; set; }
    /// <summary>APOP token, if any.</summary>
    public string? ApopToken { get; set; }
    /// <summary>Expire policy.</summary>
    public TimeSpan? ExpirePolicy { get; set; }
    /// <summary>Server implementation info.</summary>
    public ImapImplementation? Implementation { get; set; }
    /// <summary>Login delay, if any.</summary>
    public TimeSpan? LoginDelay { get; set; }
    /// <summary>Whether the client is authenticated.</summary>
    public bool IsAuthenticated { get; set; }
    /// <summary>Whether the connection is secure.</summary>
    public bool IsSecure { get; set; }
    /// <summary>The actual MailKit ImapClient instance.</summary>
    public ImapClient? Data { get; set; }
    /// <summary>Message count.</summary>
    public int Count { get; set; }
    /// <summary>Messages collection (if available).</summary>
    public ImapFolder? Messages { get; set; }
    /// <summary>Recent message count.</summary>
    public int Recent { get; set; }
    /// <summary>
    /// IMAP folder.
    /// </summary>
    public ImapFolder? Folder { get; set; }
    /// <summary>
    /// Cached folders for this connection.
    /// </summary>
    public Dictionary<string, ImapFolder> Folders { get; } = new();
}
