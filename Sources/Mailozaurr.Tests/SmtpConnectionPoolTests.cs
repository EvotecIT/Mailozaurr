using System.Threading;
using MailKit.Security;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpConnectionPoolTests
{
    private class FakeClient : ClientSmtp
    {
        public int ConnectCalls;
        private bool _connected;
        public override bool IsConnected => _connected;
        public override void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            _connected = true;
        }
    }

    [Fact]
    public void Disconnect_ReturnsClientToPool()
    {
        Smtp.PoolingEnabled = true;
        Smtp.ClearConnectionPool();
        var fake = new FakeClient();
        Smtp.ClientFactory = _ => fake;

        var smtp1 = new Smtp();
        smtp1.Connect("h", 25);
        smtp1.Disconnect();

        var smtp2 = new Smtp();
        Smtp.ClientFactory = _ => new FakeClient();
        smtp2.Connect("h", 25);

        Assert.Same(fake, smtp2.Client);
        Assert.Equal(1, fake.ConnectCalls);

        Smtp.ClientFactory = logger => new ClientSmtp();
        Smtp.ClearConnectionPool();
        Smtp.PoolingEnabled = false;
    }
}
