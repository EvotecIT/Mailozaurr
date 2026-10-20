using System.Security.Cryptography.X509Certificates;

namespace Mailozaurr;

/// <summary>Resolves an existing certificate without exporting or replacing its private key.</summary>
public static class GraphCertificateStore {
    // netstandard2.0 does not expose EphemeralKeySet. Keep its existing file-import
    // behavior; instance/store authentication never imports or exports a key.
#if NETSTANDARD2_0
    internal const X509KeyStorageFlags ImportKeyStorageFlags = X509KeyStorageFlags.DefaultKeySet;
#else
    internal const X509KeyStorageFlags ImportKeyStorageFlags = X509KeyStorageFlags.EphemeralKeySet;
#endif
    /// <summary>Loads a certificate with a private key from a Windows certificate store.</summary>
    /// <param name="thumbprint">The certificate thumbprint; spaces and invisible direction marks are ignored.</param>
    /// <param name="location">The store location.</param>
    /// <param name="name">The store name.</param>
    /// <returns>A certificate owned by the caller, which must dispose it.</returns>
    /// <exception cref="InvalidOperationException">The certificate is unavailable or has no private key.</exception>
    public static X509Certificate2 Load(string thumbprint, StoreLocation location = StoreLocation.CurrentUser, StoreName name = StoreName.My) {
        string normalized = Normalize(thumbprint);
        try {
            using var store = new X509Store(name, location);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            X509Certificate2Collection certificates = store.Certificates;
            try {
                return Select(certificates, normalized);
            } finally {
                foreach (X509Certificate2 certificate in certificates) certificate.Dispose();
            }
        } catch (Exception) {
            throw new InvalidOperationException("The Graph authentication certificate is unavailable or has no private key.");
        }
    }

    internal static X509Certificate2 Select(X509Certificate2Collection certificates, string thumbprint) {
        string normalized = Normalize(thumbprint);
        foreach (X509Certificate2 certificate in certificates) {
            if (string.Equals(certificate.Thumbprint, normalized, StringComparison.OrdinalIgnoreCase) && certificate.HasPrivateKey) {
                return new X509Certificate2(certificate);
            }
        }
        throw new InvalidOperationException("The Graph authentication certificate is unavailable or has no private key.");
    }

    private static string Normalize(string thumbprint) {
        if (string.IsNullOrWhiteSpace(thumbprint)) throw new ArgumentException("A certificate thumbprint is required.", nameof(thumbprint));
        return new string(thumbprint.Where(c => !char.IsWhiteSpace(c) && c != '\u200e' && c != '\u200f').ToArray());
    }
}
