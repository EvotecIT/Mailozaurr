using System;
using System.IO;
using Org.BouncyCastle.Bcpg.OpenPgp;
using MimeKit.Cryptography;

namespace Mailozaurr;

public class EphemeralOpenPgpContext : GnuPGContext {
    private readonly string? _password;

    public EphemeralOpenPgpContext(string? password = null) : base(CreateTempDirectory()) {
        _password = password;
    }

    private static string CreateTempDirectory() {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    protected override string GetPasswordForKey(PgpSecretKey key) {
        return _password ?? string.Empty;
    }
}
