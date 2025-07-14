using System;
using Mailozaurr;

/// <summary>
/// Example showing how to create a temporary PGP key pair.
/// </summary>
public static class GenerateTemporaryPgpKeyPair
{
    /// <summary>Runs the example.</summary>
    public static void Run()
    {
        using var keys = TemporaryPgpKeyPair.Create();
        Console.WriteLine($"Public key saved to: {keys.PublicKeyPath}");
        Console.WriteLine($"Private key saved to: {keys.PrivateKeyPath}");
    }
}
