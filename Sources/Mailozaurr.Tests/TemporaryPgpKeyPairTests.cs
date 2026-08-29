using Mailozaurr;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace Mailozaurr.Tests;

public class TemporaryPgpKeyPairTests {
    [Fact]
    public void Create_PreservesLegacyFiveParameterSignature() {
        MethodInfo? method = typeof(TemporaryPgpKeyPair).GetMethod(
            nameof(TemporaryPgpKeyPair.Create),
            new[] { typeof(string), typeof(string), typeof(int), typeof(string), typeof(bool) });

        Assert.NotNull(method);
        using var keys = TemporaryPgpKeyPair.Create("legacy@example.test", "", 2048, null, true);
        Assert.False(string.IsNullOrEmpty(keys.PassPhrase));
    }

    [Fact]
    public void Create_ReturnsFiles() {
        using var keys = TemporaryPgpKeyPair.Create("a@b.com");
        Assert.True(File.Exists(keys.PublicKeyPath));
        Assert.True(File.Exists(keys.PrivateKeyPath));
        Assert.False(string.IsNullOrEmpty(keys.PassPhrase));
    }

    [Fact]
    public void Create_RejectsWeakKeysAndExistingOutputFiles() {
        Assert.Throws<ArgumentOutOfRangeException>(() => TemporaryPgpKeyPair.Create(keySize: 1024));

        string directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string publicPath = Path.Combine(directory, "temp.pgp.pub");
        File.WriteAllText(publicPath, "do-not-overwrite");
        try {
            Assert.Throws<IOException>(() => TemporaryPgpKeyPair.Create(outputDirectory: directory));
            Assert.Equal("do-not-overwrite", File.ReadAllText(publicPath));
            Assert.False(File.Exists(Path.Combine(directory, "temp.pgp.sec")));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact(Skip = "PGP sign and encrypt requires additional configuration in CI")]
    public void KeyPair_CanSignAndEncryptMessage() {
        using var keys = TemporaryPgpKeyPair.Create("a@b.com");
        var smtp = new Smtp();
        smtp.From = "a@b.com";
        smtp.To = new object[] { "c@d.com" };
        smtp.Subject = "test";
        smtp.TextBody = "body";
        smtp.CreateMessage();
        var result = smtp.PgpSignAndEncrypt(keys.PublicKeyPath, keys.PrivateKeyPath, keys.PassPhrase, false);
        Assert.True(result.Status, result.Error);
    }

    [Fact]
    public void KeyPair_CanDecryptEncryptedMessage() {
        using var keys = TemporaryPgpKeyPair.Create("a@b.com");
        var smtp = new Smtp();
        smtp.From = "a@b.com";
        smtp.To = new object[] { "a@b.com" };
        smtp.Subject = "test";
        smtp.TextBody = "secret";
        smtp.CreateMessage();
        var encResult = smtp.PgpEncrypt(keys.PublicKeyPath);
        Assert.True(encResult.Status, encResult.Error);
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        smtp.Message.WriteTo(path);
        string decrypted = keys.DecryptToString(path);
        Assert.Contains("secret", decrypted);
        File.Delete(path);
    }

    [Fact]
    public void Dispose_RemovesGeneratedFiles() {
        string pub;
        string priv;
        string dir;
        using (var keys = TemporaryPgpKeyPair.Create("a@b.com")) {
            pub = keys.PublicKeyPath;
            priv = keys.PrivateKeyPath;
            dir = Path.GetDirectoryName(pub)!;
            Assert.True(File.Exists(pub));
            Assert.True(File.Exists(priv));
        }

        Assert.False(File.Exists(pub));
        Assert.False(File.Exists(priv));
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void Dispose_WhenDeletionFails_LogsWarnings() {
        var pair = TemporaryPgpKeyPair.Create("a@b.com");
        string originalDir = Path.GetDirectoryName(pair.PublicKeyPath)!;

        string protectedFile;
        string protectedDir;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            protectedFile = Path.Combine(Environment.SystemDirectory, "kernel32.dll");
            protectedDir = Path.Combine(Environment.SystemDirectory, "drivers");
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            protectedFile = "/System/Library/CoreServices/SystemVersion.plist";
            protectedDir = "/System/Library";
        } else {
            protectedFile = "/proc/version";
            protectedDir = "/proc/self/fd";
        }

        var type = typeof(TemporaryPgpKeyPair);
        type.GetField("<PublicKeyPath>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(pair, protectedFile);
        type.GetField("_tempDirectory", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(pair, protectedDir);

        var messages = new List<string>();
        void Handler(object? _, LogEventArgs e) => messages.Add(e.Message);
        LoggingMessages.Logger.OnWarningMessage += Handler;

        pair.Dispose();

        LoggingMessages.Logger.OnWarningMessage -= Handler;

        if (Directory.Exists(originalDir))
            Directory.Delete(originalDir, true);

        Assert.Contains(messages, static m => m.Contains("Failed to delete public key"));
        Assert.Contains(messages, static m => m.Contains("Failed to delete temporary directory"));
    }
}
