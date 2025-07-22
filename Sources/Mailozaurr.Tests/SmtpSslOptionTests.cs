using System.Reflection;
using System.Threading.Tasks;
using MailKit.Security;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpSslOptionTests
{
    private class FakeClient : ClientSmtp
    {
        public SecureSocketOptions? Options;
        public override void Connect(string host, int port, SecureSocketOptions options, System.Threading.CancellationToken cancellationToken = default)
            => Options = options;
        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, System.Threading.CancellationToken cancellationToken = default)
        {
            Options = options;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Connect_WithUseSslAndExplicitOption_DoesNotOverride()
    {
        var smtp = new Smtp();
        var fake = new FakeClient();
        typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(smtp, fake);

        smtp.Connect("h", 25, SecureSocketOptions.SslOnConnect, true);

        Assert.Equal(SecureSocketOptions.SslOnConnect, fake.Options);
    }

    [Fact]
    public async Task ConnectAsync_WithUseSslAndExplicitOption_DoesNotOverride()
    {
        var smtp = new Smtp();
        var fake = new FakeClient();
        typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(smtp, fake);

        _ = await smtp.ConnectAsync("h", 25, SecureSocketOptions.SslOnConnect, true);

        Assert.Equal(SecureSocketOptions.SslOnConnect, fake.Options);
    }
}
