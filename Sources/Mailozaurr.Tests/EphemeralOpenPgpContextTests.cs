using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Mailozaurr;
using MimeKit;
using MimeKit.Cryptography;
using Xunit;

namespace Mailozaurr.Tests;

public class EphemeralOpenPgpContextTests
{
    [Fact]
    public async Task CreateTempDirectories_AreUniqueAcrossThreads()
    {
        const int count = 20;
        var bag = new ConcurrentBag<string>();
        var field = typeof(EphemeralOpenPgpContext).GetField("_tempDirectory", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var tasks = Enumerable.Range(0, count).Select(_ => Task.Run(() =>
        {
            using var ctx = new EphemeralOpenPgpContext();
            var dir = (string)field.GetValue(ctx)!;
            bag.Add(dir);
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(count, bag.Distinct(StringComparer.Ordinal).Count());
        foreach (var dir in bag)
        {
            Assert.False(Directory.Exists(dir));
        }
    }

    [Fact]
    public void Dispose_DoesNotThrow_WhenDirectoryAlreadyDeleted()
    {
        var field = typeof(EphemeralOpenPgpContext).GetField("_tempDirectory", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var ctx = new EphemeralOpenPgpContext();
        var dir = (string)field.GetValue(ctx)!;

        Assert.True(Directory.Exists(dir));
        Directory.Delete(dir, true);
        Assert.False(Directory.Exists(dir));

        var ex = Record.Exception(() => ctx.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void ImportKeys_FromStreamAndString_AllowsEncryptionAndDecryption()
    {
        using var pair = TemporaryPgpKeyPair.Create("a@b.com", "pass");
        var publicKey = pair.ExportPublicKey();
        var privateKey = pair.ExportPrivateKey();

        using var ctx = new EphemeralOpenPgpContext(pair.PassPhrase);
        ctx.ImportKeys(publicKey);
        using var privStream = new MemoryStream(Encoding.UTF8.GetBytes(privateKey));
        ctx.ImportKeys(privStream);

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("a@b.com"));
        message.To.Add(MailboxAddress.Parse("a@b.com"));
        message.Subject = "test";
        var body = new TextPart("plain") { Text = "secret" };
        var recipients = message.To.Mailboxes;
        var encKeys = ctx.GetPublicKeys(recipients);
        message.Body = MultipartEncrypted.Encrypt(ctx, encKeys, body);

        var encrypted = (MultipartEncrypted)message.Body;
        var decrypted = encrypted.Decrypt(ctx);
        var text = ((TextPart)decrypted).Text;
        Assert.Equal("secret", text);
    }
}
