using MailKit.Net.Imap;
using MailKit.Security;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class ImapSessionServiceTests {
    private sealed class FakeImapClient : ImapClient {
        public bool AuthenticateCalled { get; set; }
        public new bool Authenticated { get; set; }
        private int _timeout;

        public override bool IsAuthenticated => Authenticated;

        public override int Timeout {
            get => _timeout;
            set => _timeout = value;
        }

        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, System.Threading.CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ConnectAsync_UsesConnectionAndAuthenticationSettings() {
        var fakeClient = new FakeImapClient();
        var previousFactory = ImapConnector.ClientFactory;
        try {
            ImapConnector.ClientFactory = () => fakeClient;
            var request = new ImapSessionRequest {
                Connection = new ImapConnectionRequest(
                    "imap.example.test",
                    1993,
                    SecureSocketOptions.SslOnConnect,
                    timeout: 4321,
                    retryCount: 0),
                UserName = "user@example.test",
                Secret = "secret",
                AuthenticateAsync = (client, _) => {
                    ((FakeImapClient)client).AuthenticateCalled = true;
                    ((FakeImapClient)client).Authenticated = true;
                    return Task.CompletedTask;
                }
            };

            var client = await ImapSessionService.ConnectAsync(request);

            Assert.Same(fakeClient, client);
            Assert.True(fakeClient.AuthenticateCalled);
            Assert.Equal(4321, fakeClient.Timeout);
        } finally {
            ImapConnector.ClientFactory = previousFactory;
        }
    }
}