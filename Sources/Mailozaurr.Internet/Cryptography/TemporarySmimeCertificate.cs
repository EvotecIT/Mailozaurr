using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Mailozaurr;

/// <summary>
/// Helper methods for generating temporary S/MIME certificates.
/// </summary>
public static class TemporarySmimeCertificate {
    /// <summary>
    /// Creates a self-signed certificate for testing purposes using the legacy signature.
    /// </summary>
    /// <param name="subjectName">Subject name of the certificate.</param>
    /// <param name="validDays">Number of days the certificate is valid.</param>
    /// <param name="outputPath">Optional path to save the PFX file. File export requires the password-aware overload.</param>
    /// <returns>A new <see cref="X509Certificate2"/> instance.</returns>
    public static X509Certificate2 CreateSelfSigned(string subjectName, int validDays, string? outputPath) =>
        CreateSelfSigned(subjectName, validDays, outputPath, outputPassword: null);

    /// <summary>
    /// Creates a self-signed certificate for testing purposes.
    /// </summary>
    /// <param name="subjectName">Subject name of the certificate.</param>
    /// <param name="validDays">Number of days the certificate is valid.</param>
    /// <param name="outputPath">Optional path to save the PFX file.</param>
    /// <param name="outputPassword">Password protecting the optional PFX file. Required when <paramref name="outputPath"/> is set.</param>
    /// <returns>A new <see cref="X509Certificate2"/> instance.</returns>
    public static X509Certificate2 CreateSelfSigned(
        string subjectName = "CN=Mailozaurr Test",
        int validDays = 1,
        string? outputPath = null,
        string? outputPassword = null) {
        if (string.IsNullOrWhiteSpace(subjectName)) throw new ArgumentException("A certificate subject is required.", nameof(subjectName));
        if (validDays <= 0) throw new ArgumentOutOfRangeException(nameof(validDays));
        if (!string.IsNullOrWhiteSpace(outputPath) && string.IsNullOrEmpty(outputPassword)) {
            throw new ArgumentException("A non-empty password is required when exporting a temporary PFX file.", nameof(outputPassword));
        }

#if NETSTANDARD2_0
        throw new NotSupportedException("Temporary S/MIME certificates require .NET Framework 4.7.2 or later.");
#else
        X509Certificate2 certificate;
#if NETFRAMEWORK
        // On .NET Framework, only use CertificateRequest on non-Windows platforms if available
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Type.GetType("System.Security.Cryptography.X509Certificates.CertificateRequest") != null) {
            certificate = CreateWithCertificateRequest(subjectName, validDays);
        } else {
            certificate = CreateWithBouncyCastle(subjectName, validDays);
        }
#else
        // On .NET Core/.NET 5+, CertificateRequest is always available
        // ALWAYS use CertificateRequest on non-Windows systems to avoid Mono compatibility issues
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)) {
            certificate = CreateWithCertificateRequest(subjectName, validDays);
        } else {
            certificate = CreateWithCertificateRequest(subjectName, validDays);
        }
#endif
        if (!string.IsNullOrWhiteSpace(outputPath)) {
            byte[] exported = certificate.Export(X509ContentType.Pfx, outputPassword);
            try {
                AttachmentFileStore.SaveToFile(
                    outputPath!,
                    stream => stream.Write(exported, 0, exported.Length),
                    AttachmentFileConflictPolicy.Fail);
            } catch {
                certificate.Dispose();
                throw;
            } finally {
                Array.Clear(exported, 0, exported.Length);
            }
        }
        return certificate;
#endif
    }

#if !NETSTANDARD2_0
    private static X509Certificate2 CreateWithCertificateRequest(string subjectName, int validDays) {
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

        // Add Enhanced Key Usage extension for S/MIME certificates
        var ekuOids = new OidCollection();
        ekuOids.Add(new Oid("1.3.6.1.5.5.7.3.2")); // Client Authentication
        ekuOids.Add(new Oid("1.3.6.1.5.5.7.3.4")); // Email Protection
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(ekuOids, true));
        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = notBefore.AddDays(validDays);
        X509Certificate2 cert = req.CreateSelfSigned(notBefore, notAfter);

        return cert;
    }
#endif

    private static X509Certificate2 CreateWithBouncyCastle(string subjectName, int validDays) {
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
        certGen.AddExtension(X509Extensions.SubjectKeyIdentifier, false, X509ExtensionUtilities.CreateSubjectKeyIdentifier(keyPair.Public));

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

        // Try different approaches for better cross-platform compatibility
        return CreateCertificateWithFallbacks(raw, pfxPassword);
    }

    /// <summary>
    /// Creates an X509Certificate2 with comprehensive fallback strategies for cross-platform compatibility.
    /// </summary>
    private static X509Certificate2 CreateCertificateWithFallbacks(byte[] pfxData, string password) {
        var exceptions = new List<Exception>();

        // Strategy 1: Try different key storage flags with password
        var flagCombinations = new[] {
            X509KeyStorageFlags.Exportable,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.DefaultKeySet,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet,
            X509KeyStorageFlags.MachineKeySet,
            X509KeyStorageFlags.UserKeySet,
            X509KeyStorageFlags.DefaultKeySet
        };

        foreach (var flags in flagCombinations) {
            try {
                return new X509Certificate2(pfxData, password, flags);
            } catch (CryptographicException ex) {
                exceptions.Add(ex);
            }
        }

        // Strategy 2: Try without password (some implementations work better this way)
        if (!string.IsNullOrEmpty(password)) {
            foreach (var flags in flagCombinations) {
                try {
                    return new X509Certificate2(pfxData, string.Empty, flags);
                } catch (CryptographicException ex) {
                    exceptions.Add(ex);
                }
            }
        }

        // Strategy 3: Try with null password
        foreach (var flags in flagCombinations) {
            try {
                return new X509Certificate2(pfxData, (string?)null, flags);
            } catch (CryptographicException ex) {
                exceptions.Add(ex);
            }
        }

        // Strategy 4: Try the simple constructor without flags
        try {
            return new X509Certificate2(pfxData, password);
        } catch (CryptographicException ex) {
            exceptions.Add(ex);
        }

        try {
            return new X509Certificate2(pfxData);
        } catch (CryptographicException ex) {
            exceptions.Add(ex);
        }

        // If all strategies fail, throw the first exception with context
        throw new CryptographicException(
            $"Failed to create X509Certificate2 on {RuntimeInformation.OSDescription}. " +
            $"Tried {exceptions.Count} different approaches. " +
            $"First error: {exceptions[0].Message}",
            exceptions[0]);
    }
}
