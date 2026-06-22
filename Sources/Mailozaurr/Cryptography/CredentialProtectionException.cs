using System;

namespace Mailozaurr;

/// <summary>
/// Indicates that protected credential data could not be decoded into a usable secret.
/// </summary>
public sealed class CredentialProtectionException : Exception {
    /// <summary>Creates a new credential protection exception.</summary>
    public CredentialProtectionException(string message, Exception? innerException = null)
        : base(message, innerException) {
    }
}
