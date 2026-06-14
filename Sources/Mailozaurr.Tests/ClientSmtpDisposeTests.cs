using MailKit;
using System.Threading;
using Xunit;

namespace Mailozaurr.Tests;

public class ClientSmtpDisposeTests {
    private class FakeClient : ClientSmtp {
        public bool DisconnectCalled;
        private bool _connected;
        public override bool IsConnected => _connected;
        public void SetConnected(bool value) => _connected = value;
        public override void Disconnect(bool quit, CancellationToken cancellationToken = default) {
            DisconnectCalled = true;
        }
    }

    [Fact]
    public void Dispose_DisconnectsWhenConnected() {
        var client = new FakeClient();
        client.SetConnected(true);
        client.Dispose();
        Assert.True(client.DisconnectCalled);
    }
}