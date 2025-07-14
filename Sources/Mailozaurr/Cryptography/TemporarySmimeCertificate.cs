using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Runtime.InteropServices;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;

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
#elif NETFRAMEWORK
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Type.GetType("System.Security.Cryptography.X509Certificates.CertificateRequest") != null)
        {
            return CreateWithCertificateRequest(subjectName, validDays, outputPath);
        }

        return CreateWithBouncyCastle(subjectName, validDays, outputPath);
#else
        // CertificateRequest is unavailable on some platforms such as Mono.
        if (Type.GetType("System.Security.Cryptography.X509Certificates.CertificateRequest") == null)
        {
            return CreateWithBouncyCastle(subjectName, validDays, outputPath);
        }

        return CreateWithCertificateRequest(subjectName, validDays, outputPath);
#endif
    }

    private static X509Certificate2 CreateWithCertificateRequest(string subjectName, int validDays, string? outputPath)
    {
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

        var flags = X509KeyStorageFlags.Exportable;
#if NET5_0_OR_GREATER
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            flags |= X509KeyStorageFlags.EphemeralKeySet;
        }
#endif

        var result = new X509Certificate2(cert.Export(X509ContentType.Pfx), string.Empty, flags);

        if (outputPath != null)
        {
            File.WriteAllBytes(outputPath, cert.Export(X509ContentType.Pfx));
        }

        return result;
    }

    private static X509Certificate2 CreateWithBouncyCastle(string subjectName, int validDays, string? outputPath)
    {
        var random = new SecureRandom();
        var keyGen = new Org.BouncyCastle.Crypto.Generators.RsaKeyPairGenerator();
        keyGen.Init(new Org.BouncyCastle.Crypto.KeyGenerationParameters(random, 2048));
        AsymmetricCipherKeyPair keyPair = keyGen.GenerateKeyPair();

        var certGen = new X509V3CertificateGenerator();
        var name = new X509Name(subjectName);
        var serial = Org.BouncyCastle.Math.BigInteger.ProbablePrime(120, random);
        certGen.SetSerialNumber(serial);
        certGen.SetIssuerDN(name);
        certGen.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
        certGen.SetNotAfter(DateTime.UtcNow.AddDays(validDays));
        certGen.SetSubjectDN(name);
        certGen.SetPublicKey(keyPair.Public);
        certGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));
        certGen.AddExtension(X509Extensions.KeyUsage, true, new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));
        certGen.AddExtension(X509Extensions.SubjectKeyIdentifier, false, new SubjectKeyIdentifierStructure(keyPair.Public));

        var signatureFactory = new Asn1SignatureFactory("SHA256WithRSA", keyPair.Private);
        Org.BouncyCastle.X509.X509Certificate bouncyCert = certGen.Generate(signatureFactory);

        var store = new Pkcs12StoreBuilder().Build();
        const string pfxPassword = "pass";
        string friendlyName = subjectName;
        var certEntry = new X509CertificateEntry(bouncyCert);
        store.SetCertificateEntry(friendlyName, certEntry);
        store.SetKeyEntry(friendlyName, new AsymmetricKeyEntry(keyPair.Private), new[] { certEntry });

        using var ms = new MemoryStream();
        store.Save(ms, pfxPassword.ToCharArray(), random);
        var raw = ms.ToArray();

        if (outputPath != null)
        {
            File.WriteAllBytes(outputPath, raw);
        }

        return new X509Certificate2(raw, pfxPassword, X509KeyStorageFlags.Exportable);
    }
}
