using System;
using System.IO;
using System.Text;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using MimeKit.Cryptography;

namespace Mailozaurr;

/// <summary>
/// Provides a <see cref="GnuPGContext"/> that stores keys in a temporary
/// directory which gets removed when the instance is disposed.
/// </summary>
/// <remarks>
/// This context is useful when you need short‑lived OpenPGP
/// encryption without leaving key material on disk.
/// </remarks>
public class EphemeralOpenPgpContext : GnuPGContext, IDisposable {
    private readonly string? _password;
    private readonly string _tempDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="EphemeralOpenPgpContext"/> class.
    /// </summary>
    /// <param name="password">Optional passphrase used when unlocking private keys.</param>
    public EphemeralOpenPgpContext(string? password = null) : base(CreateTempDirectory(out var dir)) {
        _password = password;
        _tempDirectory = dir;
    }

    /// <summary>
    /// Creates a temporary directory for the OpenPGP keyring.
    /// </summary>
    /// <param name="path">The generated directory path.</param>
    /// <returns>The created path.</returns>
    private static string CreateTempDirectory(out string path) {
        var temp = Path.GetTempPath();
        while (true) {
            path = Path.Combine(temp, Path.GetRandomFileName());
            try {
                using var fs = new FileStream(path, FileMode.CreateNew);
                fs.Close();
                File.Delete(path);
                Directory.CreateDirectory(path);
                return path;
            } catch (IOException) {
                // Collision occurred, retry with a new path
            }
        }
    }

    /// <summary>
    /// Retrieves the passphrase for the specified secret key.
    /// </summary>
    /// <param name="key">The secret key requiring a passphrase.</param>
    /// <returns>The passphrase to use.</returns>
    protected override string GetPasswordForKey(PgpSecretKey key) {
        return _password ?? string.Empty;
    }

    /// <summary>
    /// Imports PGP key material from a stream. The method automatically detects
    /// whether the stream contains a public or a private key and imports it
    /// into the context.
    /// </summary>
    /// <param name="stream">The stream containing ASCII-armored key data.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is <c>null</c>.</exception>
    public void ImportKeys(Stream stream) {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        try {
            base.Import(new PgpSecretKeyRingBundle(new ArmoredInputStream(ms)));
            return;
        } catch {
            ms.Position = 0;
            base.Import(ms);
        }
    }

    /// <summary>
    /// Imports PGP key material from a raw string.
    /// </summary>
    /// <param name="keyData">ASCII-armored key data.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="keyData"/> is <c>null</c>.</exception>
    public void ImportKeys(string keyData) {
        if (keyData == null)
            throw new ArgumentNullException(nameof(keyData));

        var bytes = Encoding.UTF8.GetBytes(keyData);
        using var ms = new MemoryStream(bytes);
        ImportKeys(ms);
    }

    /// <summary>
    /// Releases the resources used by the context and deletes the temporary directory.
    /// </summary>
    public new void Dispose() {
        base.Dispose();
        if (!Directory.Exists(_tempDirectory))
            return;

        try {
            Directory.Delete(_tempDirectory, true);
        } catch (IOException ex) {
            LoggingMessages.Logger.WriteWarning($"Failed to delete temporary directory: {ex.Message}");
        } catch (UnauthorizedAccessException ex) {
            LoggingMessages.Logger.WriteWarning($"Failed to delete temporary directory due to unauthorized access: {ex.Message}");
        }
    }
    void IDisposable.Dispose() => Dispose();
}
