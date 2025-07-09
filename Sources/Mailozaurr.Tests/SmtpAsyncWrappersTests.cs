using System.Reflection;
using System.Net;
using System.Threading.Tasks;
using MailKit.Security;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpAsyncWrappersTests
{
    private class FakeClient : ClientSmtp
    {
        public bool ConnectCalled;
        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, System.Threading.CancellationToken cancellationToken = default)
        {
            ConnectCalled = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ConnectAsync_InvokesClientConnectAsync()
    {
        var smtp = new Smtp();
        var fake = new FakeClient();
        var field = typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(smtp, fake);

        var result = await smtp.ConnectAsync("host", 25);

        Assert.True(fake.ConnectCalled);
        Assert.True(result.Status);
    }

}
