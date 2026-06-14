namespace Mailozaurr;

/// <summary>
/// Represents credentials required for Microsoft Graph authentication.
/// </summary>
/// <remarks>
/// This POCO is typically deserialized from a secure source
/// and passed to the <c>Graph</c> helper class.
/// </remarks>
public class GraphCredential {
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