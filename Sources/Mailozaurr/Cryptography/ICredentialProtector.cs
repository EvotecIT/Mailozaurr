using System;

namespace Mailozaurr;

/// <summary>
/// Provides symmetric protection for sensitive credential material.
/// </summary>
public interface ICredentialProtector {
    /// <summary>
    /// Encrypts <paramref name="plainText"/> and returns a Base64 encoded payload.
    /// </summary>
    /// <param name="plainText">Plain text secret that should be protected.</param>
    /// <returns>Base64 encoded protected payload.</returns>
    string Protect(string plainText);

    /// <summary>
    /// Decrypts <paramref name="protectedData"/> previously created by <see cref="Protect"/>.
    /// </summary>
    /// <param name="protectedData">Base64 encoded protected payload.</param>
    /// <returns>The decrypted plain text secret.</returns>
    string Unprotect(string protectedData);
}
