namespace Mailozaurr.PowerShell;

/// <summary>
/// Base class for connection information.
/// </summary>
public class ConnectionInfoBase {
    /// <summary>Indicates whether authentication succeeded.</summary>
    public bool IsConnected { get; set; }
}
