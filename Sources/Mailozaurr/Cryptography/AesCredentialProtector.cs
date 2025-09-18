using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

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

    private static readonly TimeSpan[] RetryDelays = new[] {
        TimeSpan.Zero,
        TimeSpan.FromMilliseconds(20),
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(400)
    };

    private static byte[] LoadOrCreateKey() {
        var directory = CredentialProtectionPaths.ResolveKeyDirectory();
        Directory.CreateDirectory(directory);
        var keyPath = Path.Combine(directory, KeyFileName);

        var requiresCleanup = false;

        for (var attempt = 0; attempt < RetryDelays.Length; attempt++) {
            var delay = RetryDelays[attempt];
            if (delay > TimeSpan.Zero) {
                Thread.Sleep(delay);
            }

            if (requiresCleanup) {
                if (!TryDeleteInvalidKeyFile(keyPath)) {
                    continue;
                }

                requiresCleanup = false;
            }

            if (TryReadExistingKey(keyPath, out var existingKey, out var invalidLength)) {
                return existingKey;
            }

            if (invalidLength) {
                requiresCleanup = true;
                continue;
            }

            if (TryCreateKeyFile(keyPath, out var newKey)) {
                return newKey;
            }
        }

        if (TryReadExistingKey(keyPath, out var fallbackKey, out _)) {
            return fallbackKey;
        }

        throw new IOException($"Failed to create or load credential key at '{keyPath}'.");
    }

    private static bool TryReadExistingKey(string keyPath, out byte[] key, out bool invalidLength) {
        key = Array.Empty<byte>();
        invalidLength = false;

        if (!File.Exists(keyPath)) {
            return false;
        }

        try {
            using var stream = new FileStream(keyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length != KeySizeBytes) {
                invalidLength = true;
                return false;
            }

            var buffer = new byte[KeySizeBytes];
            var offset = 0;
            while (offset < buffer.Length) {
                var bytesRead = stream.Read(buffer, offset, buffer.Length - offset);
                if (bytesRead == 0) {
                    invalidLength = true;
                    return false;
                }

                offset += bytesRead;
            }

            key = buffer;
            return true;
        } catch (IOException ex) {
            if (IsSharingViolation(ex)) {
                return false;
            }

            throw;
        } catch (UnauthorizedAccessException ex) {
            if (IsSharingViolation(ex)) {
                return false;
            }

            throw;
        }
    }

    private static bool TryCreateKeyFile(string keyPath, out byte[] key) {
        key = new byte[KeySizeBytes];
        using (var rng = RandomNumberGenerator.Create()) {
            rng.GetBytes(key);
        }

        try {
            using var stream = new FileStream(keyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(key, 0, key.Length);
            stream.Flush(true);
            return true;
        } catch (IOException ex) {
            if (IsSharingViolation(ex) || File.Exists(keyPath)) {
                return false;
            }

            throw;
        } catch (UnauthorizedAccessException ex) {
            if (IsSharingViolation(ex)) {
                return false;
            }

            throw;
        }
    }

    private static bool TryDeleteInvalidKeyFile(string keyPath) {
        try {
            if (!File.Exists(keyPath)) {
                return true;
            }

            File.Delete(keyPath);
            return true;
        } catch (IOException ex) {
            if (IsSharingViolation(ex)) {
                return false;
            }

            throw;
        } catch (UnauthorizedAccessException ex) {
            if (IsSharingViolation(ex)) {
                return false;
            }

            throw;
        }
    }

    private static bool IsSharingViolation(Exception ex) {
        const int ERROR_SHARING_VIOLATION = 32;
        const int ERROR_LOCK_VIOLATION = 33;
        const int ERROR_FILE_EXISTS = 80;
        const int ERROR_ALREADY_EXISTS = 183;
        const int ERROR_ACCESS_DENIED = 5;
        const int EACCES = 13;
        const int EAGAIN = 11;

        var code = (int)((uint)ex.HResult & 0xFFFF);

        if (code == ERROR_SHARING_VIOLATION
            || code == ERROR_LOCK_VIOLATION
            || code == ERROR_FILE_EXISTS
            || code == ERROR_ALREADY_EXISTS
            || code == ERROR_ACCESS_DENIED
            || code == EACCES
            || code == EAGAIN) {
            return true;
        }

        if (ex is IOException ioEx) {
            var message = ioEx.Message;
            if (!string.IsNullOrEmpty(message)) {
                if (message.IndexOf("sharing violation", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("used by another process", StringComparison.OrdinalIgnoreCase) >= 0) {
                    return true;
                }
            }
        }

        return false;
    }
}
