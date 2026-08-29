using MimeKit;
using MimeKit.Cryptography;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Mailozaurr;

/// <summary>
/// Provides helper methods for generating temporary PGP key pairs.
/// </summary>
public sealed class TemporaryPgpKeyPair : IDisposable {
    /// <summary>Path to the generated public key file.</summary>
    public string PublicKeyPath { get; }
    /// <summary>Path to the generated private key file.</summary>
    public string PrivateKeyPath { get; }
    /// <summary>Passphrase used for the private key.</summary>
    public string PassPhrase { get; }

    private readonly string _tempDirectory;
    private readonly bool _removeDirectory;
    private readonly bool _deleteOnDispose;

    private TemporaryPgpKeyPair(string tempDirectory, bool removeDirectory, string passPhrase, bool deleteOnDispose) {
        _tempDirectory = tempDirectory;
        _removeDirectory = removeDirectory;
        _deleteOnDispose = deleteOnDispose;
        PassPhrase = passPhrase;
        PublicKeyPath = Path.Combine(tempDirectory, "temp.pgp.pub");
        PrivateKeyPath = Path.Combine(tempDirectory, "temp.pgp.sec");
    }

    /// <summary>
    /// Generates a temporary PGP key pair stored in a transient directory.
    /// </summary>
    /// <param name="identity">Identity for the key pair.</param>
    /// <param name="passPhrase">Passphrase protecting the private key.</param>
    /// <param name="keySize">RSA key size.</param>
    /// <param name="outputDirectory">Optional output directory; if null, a random temp directory is used.</param>
    /// <param name="deleteOnDispose">When true, deletes the generated files on dispose.</param>
    /// <returns>Instance representing the created key pair.</returns>
    public static TemporaryPgpKeyPair Create(
        string identity,
        string passPhrase,
        int keySize,
        string? outputDirectory,
        bool deleteOnDispose) =>
        Create(identity, passPhrase, keySize, outputDirectory, deleteOnDispose, allowUnprotectedPrivateKey: false);

    /// <summary>
    /// Generates a temporary PGP key pair stored in a transient directory.
    /// </summary>
    /// <param name="identity">Identity for the key pair.</param>
    /// <param name="passPhrase">Passphrase protecting the private key.</param>
    /// <param name="keySize">RSA key size.</param>
    /// <param name="outputDirectory">Optional output directory; if null, a random temp directory is used.</param>
    /// <param name="deleteOnDispose">When true, deletes the generated files on dispose.</param>
    /// <param name="allowUnprotectedPrivateKey">When true, preserves an explicitly empty passphrase. Otherwise an empty passphrase is replaced with a cryptographically random value.</param>
    /// <returns>Instance representing the created key pair.</returns>
    public static TemporaryPgpKeyPair Create(
        string identity = "Mailozaurr Test",
        string passPhrase = "",
        int keySize = 2048,
        string? outputDirectory = null,
        bool deleteOnDispose = true,
        bool allowUnprotectedPrivateKey = false) {
        if (string.IsNullOrWhiteSpace(identity)) throw new ArgumentException("A key identity is required.", nameof(identity));
        if (keySize < 2048) throw new ArgumentOutOfRangeException(nameof(keySize), "RSA keys must be at least 2048 bits.");
        if (string.IsNullOrEmpty(passPhrase) && !allowUnprotectedPrivateKey) passPhrase = CreateRandomPassPhrase();

        string directory = outputDirectory ?? Path.Combine(
            Path.GetTempPath(),
            "mailozaurr-pgp-" + Guid.NewGuid().ToString("N"));
        bool removeDir = outputDirectory is null;
        bool directoryExisted = Directory.Exists(directory);
        Directory.CreateDirectory(directory);
        if (!directoryExisted) UnixFilePermissions.RestrictDirectory(directory);
        var pair = new TemporaryPgpKeyPair(directory, removeDir, passPhrase, deleteOnDispose);
        bool publicKeyCreated = false;
        bool privateKeyCreated = false;

        try {
            var generator = GenerateKeyRingGenerator(identity, passPhrase.ToCharArray(), keySize);
            using (var pubOut = CreateRestrictedFile(pair.PublicKeyPath)) {
                publicKeyCreated = true;
                using var armoredPubOut = new ArmoredOutputStream(pubOut);
                generator.GeneratePublicKeyRing().Encode(armoredPubOut);
            }

            using (var secOut = CreateRestrictedFile(pair.PrivateKeyPath)) {
                privateKeyCreated = true;
                using var armoredSecOut = new ArmoredOutputStream(secOut);
                generator.GenerateSecretKeyRing().Encode(armoredSecOut);
            }

            return pair;
        } catch {
            if (publicKeyCreated) TryDelete(pair.PublicKeyPath);
            if (privateKeyCreated) TryDelete(pair.PrivateKeyPath);
            if (removeDir) TryDeleteDirectory(directory);
            throw;
        }
    }

    private static FileStream CreateRestrictedFile(string path) {
        return UnixFilePermissions.OpenRestrictedFile(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
    }

    private static string CreateRandomPassPhrase() {
        using var random = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        random.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static void TryDelete(string path) {
        try {
            if (File.Exists(path)) File.Delete(path);
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    private static void TryDeleteDirectory(string path) {
        try {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    private static PgpKeyRingGenerator GenerateKeyRingGenerator(string identity, char[] passPhrase, int keySize) {
        var random = new SecureRandom();
        var keyGen = new RsaKeyPairGenerator();
        keyGen.Init(new RsaKeyGenerationParameters(Org.BouncyCastle.Math.BigInteger.ValueOf(0x10001), random, keySize, 12));
        var master = keyGen.GenerateKeyPair();
        var encryptor = keyGen.GenerateKeyPair();

        var masterPair = new PgpKeyPair(PublicKeyAlgorithmTag.RsaSign, master, DateTime.UtcNow);
        var encPair = new PgpKeyPair(PublicKeyAlgorithmTag.RsaEncrypt, encryptor, DateTime.UtcNow);

        var signGen = new PgpSignatureSubpacketGenerator();
        signGen.SetKeyFlags(false, PgpKeyFlags.CanSign | PgpKeyFlags.CanCertify);

        var encGen = new PgpSignatureSubpacketGenerator();
        encGen.SetKeyFlags(false, PgpKeyFlags.CanEncryptCommunications | PgpKeyFlags.CanEncryptStorage);

        var generator = new PgpKeyRingGenerator(PgpSignature.DefaultCertification, masterPair, identity,
            SymmetricKeyAlgorithmTag.Aes256, passPhrase, true, signGen.Generate(), null, random);
        generator.AddSubKey(encPair, encGen.Generate(), null);
        return generator;
    }

    /// <inheritdoc />
    public void Dispose() {
        if (_deleteOnDispose) {
            if (File.Exists(PublicKeyPath)) {
                try {
                    File.Delete(PublicKeyPath);
                } catch (IOException ex) {
                    LoggingMessages.Logger.WriteWarning($"Failed to delete public key: {ex.Message}");
                } catch (UnauthorizedAccessException ex) {
                    LoggingMessages.Logger.WriteWarning($"Failed to delete public key due to unauthorized access: {ex.Message}");
                }
            }

            if (File.Exists(PrivateKeyPath)) {
                try {
                    File.Delete(PrivateKeyPath);
                } catch (IOException ex) {
                    LoggingMessages.Logger.WriteWarning($"Failed to delete private key: {ex.Message}");
                } catch (UnauthorizedAccessException ex) {
                    LoggingMessages.Logger.WriteWarning($"Failed to delete private key due to unauthorized access: {ex.Message}");
                }
            }

            if (_removeDirectory && Directory.Exists(_tempDirectory)) {
                try {
                    Directory.Delete(_tempDirectory, true);
                } catch (IOException ex) {
                    LoggingMessages.Logger.WriteWarning($"Failed to delete temporary directory: {ex.Message}");
                } catch (UnauthorizedAccessException ex) {
                    LoggingMessages.Logger.WriteWarning($"Failed to delete temporary directory due to unauthorized access: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Decrypts an encrypted MIME message using this key pair.
    /// </summary>
    /// <param name="messagePath">Path to the encrypted message file.</param>
    /// <returns>Decrypted message body text.</returns>
    public string DecryptToString(string messagePath) {
        MimeMessage message = MimeMessage.Load(messagePath);
        using var ctx = new EphemeralOpenPgpContext(PassPhrase);
        using (var sec = File.OpenRead(PrivateKeyPath))
            ctx.Import(new PgpSecretKeyRingBundle(new ArmoredInputStream(sec)));

        if (message.Body is MultipartEncrypted encrypted) {
            var decrypted = encrypted.Decrypt(ctx);
            if (decrypted is TextPart text) {
                return text.Text ?? string.Empty;
            }
        }

        throw new InvalidOperationException("Message is not PGP encrypted");
    }
}
