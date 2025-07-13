using System;
using System.IO;
using System.Runtime.InteropServices;
#if !UNIX
using System.Security.AccessControl;
using System.Security.Principal;
#endif
using Org.BouncyCastle.Bcpg.OpenPgp;
using MimeKit.Cryptography;

namespace Mailozaurr;

/// <summary>
/// Provides a <see cref="GnuPGContext"/> that stores keys in a temporary
/// directory which gets removed when the instance is disposed.
/// </summary>
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
                SetRestrictivePermissions(path);
                return path;
            } catch (IOException) {
                // Collision occurred, retry with a new path
            }
        }
    }

#if UNIX
    [DllImport("libc", SetLastError = true)]
    private static extern int chmod(string path, int mode);
#endif

    private static void SetRestrictivePermissions(string directory) {
#if UNIX
        try {
            _ = chmod(directory, Convert.ToInt32("700", 8));
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Failed to set permissions on temporary directory: {ex.Message}");
        }
#else
        try {
            var info = new DirectoryInfo(directory);
            var current = WindowsIdentity.GetCurrent().User!;
            var security = new DirectorySecurity();
            security.SetOwner(current);
            security.SetAccessRule(new FileSystemAccessRule(
                current,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            info.SetAccessControl(security);
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Failed to set permissions on temporary directory: {ex.Message}");
        }
#endif
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
