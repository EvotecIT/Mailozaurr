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
    private const int MacSizeBytes = 32;
    private static readonly byte[] PayloadPrefix = { (byte)'M', (byte)'Z', (byte)'C', 2 };
    private static readonly object KeyFileSyncRoot = new();

    private readonly byte[] key;

    public AesCredentialProtector() {
        key = LoadOrCreateKey();
    }

    public string Protect(string plainText) {
        if (plainText == null) {
            throw new ArgumentNullException(nameof(plainText));
        }

        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
        var keys = DeriveKeys();

        using var aes = Aes.Create();
        aes.Key = keys.EncryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var cipherBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        var payloadWithoutMac = new byte[PayloadPrefix.Length + aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(PayloadPrefix, 0, payloadWithoutMac, 0, PayloadPrefix.Length);
        Buffer.BlockCopy(aes.IV, 0, payloadWithoutMac, PayloadPrefix.Length, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, payloadWithoutMac, PayloadPrefix.Length + aes.IV.Length, cipherBytes.Length);

        using var hmac = new HMACSHA256(keys.AuthenticationKey);
        var mac = hmac.ComputeHash(payloadWithoutMac);

        var payload = new byte[payloadWithoutMac.Length + mac.Length];
        Buffer.BlockCopy(payloadWithoutMac, 0, payload, 0, payloadWithoutMac.Length);
        Buffer.BlockCopy(mac, 0, payload, payloadWithoutMac.Length, mac.Length);

        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedData) {
        if (protectedData == null) {
            throw new ArgumentNullException(nameof(protectedData));
        }

        var payload = Convert.FromBase64String(protectedData);
        if (IsAuthenticatedPayload(payload)) {
            return UnprotectAuthenticated(payload);
        }

        return UnprotectLegacy(payload);
    }

    private string UnprotectAuthenticated(byte[] payload) {
        if (payload.Length < PayloadPrefix.Length + IvSizeBytes + MacSizeBytes) {
            throw new CryptographicException("Protected payload is too short.");
        }

        var keys = DeriveKeys();
        var payloadLengthWithoutMac = payload.Length - MacSizeBytes;
        var storedMac = new byte[MacSizeBytes];
        Buffer.BlockCopy(payload, payloadLengthWithoutMac, storedMac, 0, storedMac.Length);

        using var hmac = new HMACSHA256(keys.AuthenticationKey);
        var computedMac = hmac.ComputeHash(payload, 0, payloadLengthWithoutMac);
        if (!FixedTimeEquals(storedMac, computedMac)) {
            throw new CryptographicException("Protected payload authentication failed.");
        }

        var iv = new byte[IvSizeBytes];
        Buffer.BlockCopy(payload, PayloadPrefix.Length, iv, 0, IvSizeBytes);
        var cipherOffset = PayloadPrefix.Length + IvSizeBytes;
        var cipherLength = payloadLengthWithoutMac - cipherOffset;
        var cipherBytes = new byte[cipherLength];
        Buffer.BlockCopy(payload, cipherOffset, cipherBytes, 0, cipherBytes.Length);

        return Decrypt(keys.EncryptionKey, iv, cipherBytes);
    }

    private string UnprotectLegacy(byte[] payload) {
        if (payload.Length < IvSizeBytes) {
            throw new CryptographicException("Protected payload is too short.");
        }

        var iv = new byte[IvSizeBytes];
        Buffer.BlockCopy(payload, 0, iv, 0, IvSizeBytes);
        var cipherBytes = new byte[payload.Length - IvSizeBytes];
        Buffer.BlockCopy(payload, IvSizeBytes, cipherBytes, 0, cipherBytes.Length);

        return Decrypt(key, iv, cipherBytes);
    }

    private string Decrypt(byte[] encryptionKey, byte[] iv, byte[] cipherBytes) {
        using var aes = Aes.Create();
        aes.Key = encryptionKey;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var plaintextBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private CredentialProtectionKeys DeriveKeys() {
        using var hmac = new HMACSHA512(key);
        var material = hmac.ComputeHash(Encoding.UTF8.GetBytes("Mailozaurr credential protection v2"));
        var encryptionKey = new byte[KeySizeBytes];
        var authenticationKey = new byte[KeySizeBytes];
        Buffer.BlockCopy(material, 0, encryptionKey, 0, encryptionKey.Length);
        Buffer.BlockCopy(material, encryptionKey.Length, authenticationKey, 0, authenticationKey.Length);
        return new CredentialProtectionKeys(encryptionKey, authenticationKey);
    }

    private static bool IsAuthenticatedPayload(byte[] payload) {
        if (payload.Length < PayloadPrefix.Length) {
            return false;
        }

        for (var i = 0; i < PayloadPrefix.Length; i++) {
            if (payload[i] != PayloadPrefix[i]) {
                return false;
            }
        }

        return true;
    }

    private static bool FixedTimeEquals(byte[] left, byte[] right) {
        if (left.Length != right.Length) {
            return false;
        }

        var diff = 0;
        for (var i = 0; i < left.Length; i++) {
            diff |= left[i] ^ right[i];
        }

        return diff == 0;
    }

    private sealed class CredentialProtectionKeys {
        public CredentialProtectionKeys(byte[] encryptionKey, byte[] authenticationKey) {
            EncryptionKey = encryptionKey;
            AuthenticationKey = authenticationKey;
        }

        public byte[] EncryptionKey { get; }

        public byte[] AuthenticationKey { get; }
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
        lock (KeyFileSyncRoot) {
            return LoadOrCreateKeyCore();
        }
    }

    private static byte[] LoadOrCreateKeyCore() {
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

        var directory = Path.GetDirectoryName(keyPath) ?? ".";
        var tempPath = Path.Combine(directory, $"{KeyFileName}.{Guid.NewGuid():N}.tmp");

        try {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                stream.Write(key, 0, key.Length);
                stream.Flush(true);
            }

            File.Move(tempPath, keyPath);
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
        } finally {
            TryDeleteTempKeyFile(tempPath);
        }
    }

    private static void TryDeleteTempKeyFile(string tempPath) {
        try {
            if (File.Exists(tempPath)) {
                File.Delete(tempPath);
            }
        } catch {
            // Best-effort cleanup only; the final key file is the source of truth.
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