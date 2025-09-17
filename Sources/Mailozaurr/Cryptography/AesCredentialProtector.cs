#if !WINDOWS
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Mailozaurr;

internal sealed class AesCredentialProtector : ICredentialProtector {
    private const string KeyFileName = "credential.key";
    private const int KeySizeBytes = 32;
    private const int IvSizeBytes = 16;

    private readonly byte[] key;

    public AesCredentialProtector() {
        key = LoadOrCreateKey();
    }

    public string Protect(string plainText) {
        if (plainText == null) {
            throw new ArgumentNullException(nameof(plainText));
        }

        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var cipherBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        var payload = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedData) {
        if (protectedData == null) {
            throw new ArgumentNullException(nameof(protectedData));
        }

        var payload = Convert.FromBase64String(protectedData);
        if (payload.Length < IvSizeBytes) {
            throw new CryptographicException("Protected payload is too short.");
        }

        var iv = new byte[IvSizeBytes];
        Buffer.BlockCopy(payload, 0, iv, 0, IvSizeBytes);
        var cipherBytes = new byte[payload.Length - IvSizeBytes];
        Buffer.BlockCopy(payload, IvSizeBytes, cipherBytes, 0, cipherBytes.Length);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var plaintextBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private static byte[] LoadOrCreateKey() {
        var directory = CredentialProtectionPaths.ResolveKeyDirectory();
        Directory.CreateDirectory(directory);
        var keyPath = Path.Combine(directory, KeyFileName);

        if (File.Exists(keyPath)) {
            var existingKey = File.ReadAllBytes(keyPath);
            if (existingKey.Length == KeySizeBytes) {
                return existingKey;
            }
        }

        var newKey = new byte[KeySizeBytes];
        using (var rng = RandomNumberGenerator.Create()) {
            rng.GetBytes(newKey);
        }
        File.WriteAllBytes(keyPath, newKey);
        return newKey;
    }
}
#endif
