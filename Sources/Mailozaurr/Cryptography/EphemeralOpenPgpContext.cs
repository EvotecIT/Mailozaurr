using System;
using System.IO;
using Org.BouncyCastle.Bcpg.OpenPgp;
using MimeKit.Cryptography;

namespace Mailozaurr;

public class EphemeralOpenPgpContext : GnuPGContext {
    private readonly string? _password;
    private readonly string _tempDirectory;

    public EphemeralOpenPgpContext(string? password = null) : base(CreateTempDirectory(out var dir)) {
        _password = password;
        _tempDirectory = dir;
    }

    private static string CreateTempDirectory(out string path) {
        path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    protected override string GetPasswordForKey(PgpSecretKey key) {
        return _password ?? string.Empty;
    }

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
