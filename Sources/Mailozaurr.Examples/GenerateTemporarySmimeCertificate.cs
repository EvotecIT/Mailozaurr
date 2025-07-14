using System;
using Mailozaurr;

/// <summary>
/// Example showing how to create a temporary S/MIME certificate.
/// </summary>
public static class GenerateTemporarySmimeCertificate
{
    /// <summary>Runs the example.</summary>
    public static void Run()
    {
        using var cert = TemporarySmimeCertificate.CreateSelfSigned();
        Console.WriteLine($"Temporary certificate created: {cert.Subject}");
    }
}
