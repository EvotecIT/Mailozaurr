using System;
using System.IO;
using Org.BouncyCastle.Bcpg.OpenPgp;
using MimeKit.Cryptography;

namespace Mailozaurr;

/// <summary>
/// Provides a <see cref="GnuPGContext"/> that stores keys in a temporary
/// directory which gets removed when the instance is disposed.
/// </summary>
public class EphemeralOpenPgpContext : GnuPGContext {
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
        path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
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
    /// Releases the resources used by the context and deletes the temporary directory.
    /// </summary>
    public new void Dispose() {
        base.Dispose();
        try {
            Directory.Delete(_tempDirectory, true);
        } catch (IOException ex) {
            LoggingMessages.Logger.WriteWarning($"Failed to delete temporary directory: {ex.Message}");
        } catch (UnauthorizedAccessException ex) {
            LoggingMessages.Logger.WriteWarning($"Failed to delete temporary directory due to unauthorized access: {ex.Message}");
        }
    }
}
