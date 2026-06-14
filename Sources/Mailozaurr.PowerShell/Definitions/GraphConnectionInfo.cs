using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Represents an authenticated Microsoft Graph connection.
/// </summary>
public class GraphConnectionInfo : ConnectionInfoBase {
    /// <summary>Graph credentials.</summary>
    public GraphCredential Credential { get; set; } = null!;

    /// <summary>
    /// OAuth credential obtained using device code or on-behalf-of flow.
    /// </summary>
    public OAuthCredential? OAuthCredential { get; set; }
}