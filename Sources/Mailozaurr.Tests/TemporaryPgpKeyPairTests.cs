using Mailozaurr;
using System.IO;
using Xunit;

namespace Mailozaurr.Tests;

public class TemporaryPgpKeyPairTests
{
    [Fact]
    public void Create_ReturnsFiles()
    {
        using var keys = TemporaryPgpKeyPair.Create("a@b.com");
        Assert.True(File.Exists(keys.PublicKeyPath));
        Assert.True(File.Exists(keys.PrivateKeyPath));
    }

    [Fact(Skip = "PGP sign and encrypt requires additional configuration in CI" )]
    public void KeyPair_CanSignAndEncryptMessage()
    {
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
    public void KeyPair_CanDecryptEncryptedMessage()
    {
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
    public void Dispose_RemovesGeneratedFiles()
    {
        string pub;
        string priv;
        string dir;
        using (var keys = TemporaryPgpKeyPair.Create("a@b.com"))
        {
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
}
