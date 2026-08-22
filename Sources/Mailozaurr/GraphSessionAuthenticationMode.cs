namespace Mailozaurr;

/// <summary>
/// Describes how a Microsoft Graph session credential was obtained when that fact is known
/// independently of access-token claims.
/// </summary>
public enum GraphSessionAuthenticationMode {
    /// <summary>The credential may be delegated or application-only.</summary>
    Unknown,

    /// <summary>The credential was obtained through a delegated interactive flow.</summary>
    Delegated,

    /// <summary>The credential was obtained through a confidential-client application flow.</summary>
    Application
}
