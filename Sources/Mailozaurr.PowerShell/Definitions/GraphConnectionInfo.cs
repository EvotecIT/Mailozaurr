namespace Mailozaurr.PowerShell;

/// <summary>
/// Represents an authenticated Microsoft Graph connection.
/// </summary>
public class GraphConnectionInfo {
    /// <summary>Graph credentials.</summary>
    public GraphCredential Credential { get; set; }
    /// <summary>Indicates whether authentication succeeded.</summary>
    public bool IsConnected { get; set; }
}
