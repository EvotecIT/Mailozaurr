using System;
using System.Collections.Generic;
using System.IO;
using MailKit;
using MailKit.Net.Pop3;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Represents the result of a successful POP3 connection, including the client and connection details.
/// </summary>
public class PopConnectionInfo : ConnectionInfoBase {
    /// <summary>POP3 server URI.</summary>
    public string Uri { get; set; } = string.Empty;
    /// <summary>Authentication mechanisms supported by the server.</summary>
    public ISet<string>? AuthenticationMechanisms { get; set; }
    /// <summary>Server capabilities.</summary>
    public Pop3Capabilities Capabilities { get; set; }
    /// <summary>The underlying stream.</summary>
    public Stream? Stream { get; set; }
    /// <summary>Current state of the connection.</summary>
    public object? State { get; set; }
    /// <summary>APOP token, if any.</summary>
    public string? ApopToken { get; set; }
    /// <summary>Expire policy.</summary>
    public TimeSpan? ExpirePolicy { get; set; }
    /// <summary>Server implementation info.</summary>
    public object? Implementation { get; set; }
    /// <summary>Login delay, if any.</summary>
    public TimeSpan? LoginDelay { get; set; }
    /// <summary>Whether the client is authenticated.</summary>
    public bool IsAuthenticated { get; set; }
    /// <summary>Whether the connection is secure.</summary>
    public bool IsSecure { get; set; }
    /// <summary>The actual MailKit Pop3Client instance.</summary>
    public Pop3Client? Data { get; set; }
    /// <summary>Message count.</summary>
    public int Count { get; set; }
    /// <summary>Messages collection (if available).</summary>
    public object? Messages { get; set; }
    /// <summary>Recent message count.</summary>
    public int Recent { get; set; }
}
