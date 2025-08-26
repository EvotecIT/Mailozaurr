using MailKit.Security;
using System.Threading;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpConnectionPoolTests {
    private class FakeClient : ClientSmtp {
        public int ConnectCalls;
        private bool _connected;
        public override bool IsConnected => _connected;
        public void SetConnected(bool value) => _connected = value;
        public override void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {
            ConnectCalls++;
            _connected = true;
        }
    }

    [Fact]
    public void Disconnect_ReturnsClientToPool() {
        SmtpConnectionPool.SetPoolingEnabled(true);
        SmtpConnectionPool.ClearConnectionPool();
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
        SmtpConnectionPool.ClearConnectionPool();
        SmtpConnectionPool.SetPoolingEnabled(false);
    }

    [Fact]
    public void ReturnClosedClient_IsDiscarded() {
        SmtpConnectionPool.SetPoolingEnabled(true);
        SmtpConnectionPool.ClearConnectionPool();

        var fake = new FakeClient();
        fake.SetConnected(false);
        SmtpConnectionPool.ReturnClient("h", 25, fake);

        var pooled = SmtpConnectionPool.TryRentClient("h", 25);
        Assert.Null(pooled);

        SmtpConnectionPool.ClearConnectionPool();
        SmtpConnectionPool.SetPoolingEnabled(false);
    }
}