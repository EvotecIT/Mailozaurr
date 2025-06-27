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

    /// <summary>
    /// Time when the access token expires.
    /// </summary>
    public DateTimeOffset ExpiresOn { get; set; }

    /// <summary>
    /// The refresh token, if available.
    /// </summary>
    public string? RefreshToken { get; set; }
}
