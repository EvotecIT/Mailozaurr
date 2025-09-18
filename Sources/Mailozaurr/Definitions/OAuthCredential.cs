namespace Mailozaurr;

/// <summary>
/// Represents OAuth credentials.
/// </summary>
/// <remarks>
/// This structure is used by various helper classes to cache
/// tokens acquired from identity providers.
/// </remarks>
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

    /// <summary>
    /// Identifier of the OAuth client used to acquire the token.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Secret associated with the OAuth client used to acquire the token.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Raw JSON payload containing service account credentials used for delegated access.
    /// </summary>
    public string? ServiceAccountJson { get; set; }

    /// <summary>
    /// Optional subject impersonated when using service account credentials.
    /// </summary>
    public string? ServiceAccountSubject { get; set; }
}
