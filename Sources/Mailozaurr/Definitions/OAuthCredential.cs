namespace Mailozaurr;

/// <summary>
/// Represents OAuth credentials.
/// </summary>
public class OAuthCredential {
    /// <summary>
    /// The username associated with the OAuth credential.
    /// </summary>
    public string UserName { get; set; }
    /// <summary>
    /// The access token for the OAuth credential.
    /// </summary>
    public string AccessToken { get; set; }
}
