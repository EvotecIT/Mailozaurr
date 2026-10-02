using System.Net;
using System.Text;
using MailKit.Security;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public sealed class SmtpPoolAuthenticationTests {
    [Fact]
    public async Task WarmedAccount_ReusesAuthenticationButAnonymousHintCannotRentIt() {
        var originalFactory = Smtp.ClientFactory;
        SmtpConnectionPool.ClearConnectionPool();
        Smtp.ClientFactory = _ => new AuthenticatedClient();
        try {
            AuthenticatedClient transport;
            var first = new Smtp { UseConnectionPool = true };
            try {
                Assert.True((await first.ConnectAndAuthenticateAsync("smtp.example.test", 587, "account-A", "secret")).IsSuccess);
                transport = (AuthenticatedClient)first.Client;
            } finally { first.Dispose(); }
            var second = new Smtp { UseConnectionPool = true };
            try {
                Assert.True((await second.ConnectAndAuthenticateAsync("smtp.example.test", 587, "account-A", "secret")).IsSuccess);
                Assert.Same(transport, second.Client);
                Assert.Equal(1, transport.Connects);
                Assert.Equal(1, transport.Authentications);
            } finally { second.Dispose(); }
            var anonymous = new Smtp { UseConnectionPool = true, ConnectionPoolIdentity = "account-A" };
            try {
                Assert.True((await anonymous.ConnectAsync("smtp.example.test", 587)).Status);
                Assert.NotSame(transport, anonymous.Client);
                Assert.False(anonymous.Client.IsAuthenticated);
            } finally { anonymous.Dispose(); }
        } finally {
            Smtp.ClientFactory = originalFactory;
            SmtpConnectionPool.ClearConnectionPool();
        }
    }

    private sealed class AuthenticatedClient : ClientSmtp {
        private bool connected;
        private bool authenticated;
        internal int Connects;
        internal int Authentications;
        public override bool IsConnected => connected;
        public override bool IsAuthenticated => authenticated;
        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {
            Connects++; connected = true; return Task.CompletedTask;
        }
        public override Task AuthenticateAsync(Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default) {
            Authentications++; authenticated = true; return Task.CompletedTask;
        }
        public override void Disconnect(bool quit, CancellationToken cancellationToken = default) { connected = authenticated = false; }
    }
}
