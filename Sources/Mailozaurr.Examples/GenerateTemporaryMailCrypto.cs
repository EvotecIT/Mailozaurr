using System;
using Mailozaurr;

/// <summary>
/// Example showing how to create temporary cryptographic material.
/// </summary>
public static class GenerateTemporaryMailCrypto
{
    /// <summary>Runs the example.</summary>
    public static void Run()
    {
        using var keys = TemporaryPgpKeyPair.Create(outputDirectory: "pgp", deleteOnDispose: false);
        Console.WriteLine($"PGP public key: {keys.PublicKeyPath}");
        using var cert = TemporarySmimeCertificate.CreateSelfSigned(outputPath: "cert.pfx");
        Console.WriteLine($"S/MIME certificate: {cert.Subject}");
    }
}
