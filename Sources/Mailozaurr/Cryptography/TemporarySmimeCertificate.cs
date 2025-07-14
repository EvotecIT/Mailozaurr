using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IO;

namespace Mailozaurr;

/// <summary>
/// Helper methods for generating temporary S/MIME certificates.
/// </summary>
public static class TemporarySmimeCertificate
{
    /// <summary>
    /// Creates a self-signed certificate for testing purposes.
    /// </summary>
    /// <param name="subjectName">Subject name of the certificate.</param>
    /// <param name="validDays">Number of days the certificate is valid.</param>
    /// <returns>A new <see cref="X509Certificate2"/> instance.</returns>
    public static X509Certificate2 CreateSelfSigned(string subjectName = "CN=Mailozaurr Test", int validDays = 1, string? outputPath = null)
    {
#if NETSTANDARD2_0
        throw new NotSupportedException("Temporary S/MIME certificates require .NET Framework 4.7.2 or later.");
#else
        using RSA rsa = RSA.Create();
        rsa.KeySize = 2048;
        var req = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.DigitalSignature |
                System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyEncipherment,
                true));
        req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));
        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = notBefore.AddDays(validDays);
        using X509Certificate2 cert = req.CreateSelfSigned(notBefore, notAfter);
#if NET5_0_OR_GREATER
        var result = new X509Certificate2(cert.Export(X509ContentType.Pfx), string.Empty, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
#else
        var result = new X509Certificate2(cert.Export(X509ContentType.Pfx), string.Empty, X509KeyStorageFlags.Exportable);
#endif
        if (outputPath != null)
        {
            File.WriteAllBytes(outputPath, cert.Export(X509ContentType.Pfx));
        }
        return result;
#endif
    }
}
