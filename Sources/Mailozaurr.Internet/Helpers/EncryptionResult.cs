namespace Mailozaurr;

/// <summary>
/// Helper class to return encryption results and the IV used to do the encryption.
/// </summary>
internal class EncryptionResult {
    internal EncryptionResult(string encrypted, string IV) {
        EncryptedData = encrypted;
        this.IV = IV;
    }

    /// <summary>Gets the encrypted data.</summary>
    internal string EncryptedData { get; }

    /// <summary>Gets the IV used to encrypt the data.</summary>
    internal string IV { get; }
}