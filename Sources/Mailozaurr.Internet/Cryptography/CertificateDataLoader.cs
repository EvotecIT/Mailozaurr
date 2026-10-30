using System.Security.Cryptography.X509Certificates;

namespace Mailozaurr;

/// <summary>Uses the platform certificate loaders while retaining older framework support.</summary>
internal static class CertificateDataLoader {
    internal static X509Certificate2 LoadCertificate(byte[] encodedCertificate) {
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificate(encodedCertificate);
#else
        return new X509Certificate2(encodedCertificate);
#endif
    }

    internal static X509Certificate2 LoadPkcs12(byte[] data, string? password,
        X509KeyStorageFlags flags = X509KeyStorageFlags.DefaultKeySet) {
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(data, password, flags);
#else
        return new X509Certificate2(data, password, flags);
#endif
    }

    internal static X509Certificate2 LoadPkcs12File(string path, string? password,
        X509KeyStorageFlags flags = X509KeyStorageFlags.DefaultKeySet) {
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12FromFile(path, password, flags);
#else
        return new X509Certificate2(path, password, flags);
#endif
    }
}
