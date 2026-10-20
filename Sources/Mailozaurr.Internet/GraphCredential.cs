namespace Mailozaurr;

/// <summary>
/// Represents credentials required for Microsoft Graph authentication.
/// </summary>
/// <remarks>
/// This POCO is typically deserialized from a secure source
/// and passed to the <c>Graph</c> helper class.
/// </remarks>
public class GraphCredential {
    /// <summary>Gets or sets a caller-owned certificate with its private key. The key need not be exportable.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public System.Security.Cryptography.X509Certificates.X509Certificate2? Certificate { get; set; }

    /// <summary>Gets or sets a certificate thumbprint to resolve from the specified Windows certificate store.</summary>
    public string? CertificateThumbprint { get; set; }

    /// <summary>Gets or sets the certificate store location. Defaults to the current user.</summary>
    public System.Security.Cryptography.X509Certificates.StoreLocation CertificateStoreLocation { get; set; } = System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser;

    /// <summary>Gets or sets the certificate store name. Defaults to the personal store.</summary>
    public System.Security.Cryptography.X509Certificates.StoreName CertificateStoreName { get; set; } = System.Security.Cryptography.X509Certificates.StoreName.My;

    /// <summary>
    /// Gets or sets the application (client) identifier.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the directory (tenant) identifier.
    /// </summary>
    public string DirectoryId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client secret if using secret-based auth.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets an access token for delegated Microsoft Graph operations.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the path to a certificate used for authentication.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the certificate password.
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Gets or sets certificate bytes when provided programmatically.
    /// </summary>
    public byte[]? CertificateBytes { get; set; }

    /// <summary>
    /// Gets or sets the PEM certificate path, if applicable.
    /// </summary>
    public string? CertificatePemPath { get; set; }
}
