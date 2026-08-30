using Mailozaurr;
using System;

/// <summary>
/// Example showing how to create temporary cryptographic material.
/// </summary>
public static class GenerateTemporaryMailCrypto {
    /// <summary>Runs the example.</summary>
    public static void Run() {
        using var keys = TemporaryPgpKeyPair.Create(outputDirectory: "pgp", deleteOnDispose: false);
        Console.WriteLine($"PGP public key: {keys.PublicKeyPath}");
        string certificatePassword = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        using var cert = TemporarySmimeCertificate.CreateSelfSigned(
            outputPath: "cert.pfx",
            outputPassword: certificatePassword);
        Console.WriteLine($"S/MIME certificate: {cert.Subject}");
    }
}
