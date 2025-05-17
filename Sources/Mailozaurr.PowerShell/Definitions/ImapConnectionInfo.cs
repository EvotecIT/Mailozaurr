using MailKit.Net.Imap;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Represents the result of a successful IMAP connection, including the client and connection details.
/// </summary>
public class ImapConnectionInfo {
    /// <summary>IMAP server URI.</summary>
    public string Uri { get; set; }
    /// <summary>Authentication mechanisms supported by the server.</summary>
    public object AuthenticationMechanisms { get; set; }
    /// <summary>Server capabilities.</summary>
    public object Capabilities { get; set; }
    /// <summary>The underlying stream.</summary>
    public object Stream { get; set; }
    /// <summary>Current state of the connection.</summary>
    public object State { get; set; }
    /// <summary>Whether the client is connected.</summary>
    public bool IsConnected { get; set; }
    /// <summary>APOP token, if any.</summary>
    public object ApopToken { get; set; }
    /// <summary>Expire policy.</summary>
    public object ExpirePolicy { get; set; }
    /// <summary>Server implementation info.</summary>
    public object Implementation { get; set; }
    /// <summary>Login delay, if any.</summary>
    public object LoginDelay { get; set; }
    /// <summary>Whether the client is authenticated.</summary>
    public bool IsAuthenticated { get; set; }
    /// <summary>Whether the connection is secure.</summary>
    public bool IsSecure { get; set; }
    /// <summary>The actual MailKit ImapClient instance.</summary>
    public ImapClient Data { get; set; }
    /// <summary>Message count.</summary>
    public int Count { get; set; }
    /// <summary>Messages collection (if available).</summary>
    public object Messages { get; set; }
    /// <summary>Recent message count.</summary>
    public int Recent { get; set; }
    /// <summary>
    /// IMAP folder.
    /// </summary>
    public ImapFolder Folder { get; set; }
}