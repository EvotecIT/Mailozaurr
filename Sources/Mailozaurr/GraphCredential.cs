namespace Mailozaurr;

/// <summary>
/// Represents credentials required for Microsoft Graph authentication.
/// </summary>
public class GraphCredential {
    public string ClientId { get; set; }
    public string DirectoryId { get; set; }
    public string? ClientSecret { get; set; }
    public string? CertificatePath { get; set; }
    public string? CertificatePassword { get; set; }
    public byte[]? CertificateBytes { get; set; }
    public string? CertificatePemPath { get; set; }
}
