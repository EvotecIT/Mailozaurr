namespace Mailozaurr.PowerShell;

/// <summary>
/// Represents an authenticated Microsoft Graph connection.
/// </summary>
public class GraphConnectionInfo : ConnectionInfoBase {
    /// <summary>Graph credentials.</summary>
    public GraphCredential Credential { get; set; }
}
