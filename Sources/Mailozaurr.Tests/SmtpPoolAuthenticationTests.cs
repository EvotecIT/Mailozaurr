using System.Net;
using System.Text;
using MailKit.Security;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public sealed class SmtpPoolAuthenticationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OAuthWithUnsupportedCredentialType_DoesNotReportAuthenticationSuccess(bool asynchronous) {
        var originalFactory = Smtp.ClientFactory;
        Smtp.ClientFactory = _ => new AuthenticatedClient();
        var smtp = new Smtp();
        try {
            Assert.True((await smtp.ConnectAsync("smtp.example.test", 587)).Status);
            var credentials = new CredentialCache();
            var result = asynchronous
                ? await smtp.AuthenticateAsync(credentials, isOAuth: true)
                : smtp.Authenticate(credentials, isOAuth: true);
            Assert.False(result.Status);
            Assert.False(smtp.Client.IsAuthenticated);
            Assert.Equal(0, ((AuthenticatedClient)smtp.Client).Authentications);
            Assert.Contains("NetworkCredential", result.Error);
        } finally {
            smtp.Dispose();
            Smtp.ClientFactory = originalFactory;
        }
    }

    [Theory]
    [InlineData("wrong-secret", ProtocolAuthMode.Basic)]
    [InlineData("secret", ProtocolAuthMode.OAuth2)]
    [InlineData("", ProtocolAuthMode.Basic)]
    public async Task ChangedAuthenticationContext_DoesNotReuseAuthenticatedSession(string secret, ProtocolAuthMode mode) {
        var originalFactory = Smtp.ClientFactory;
        SmtpConnectionPool.ClearConnectionPool();
        Smtp.ClientFactory = _ => new AuthenticatedClient();
        var first = new Smtp { UseConnectionPool = true };
        var changed = new Smtp { UseConnectionPool = true };
        try {
            Assert.True((await first.ConnectAndAuthenticateAsync("smtp.example.test", 587, "account-A", "secret")).IsSuccess);
            var transport = first.Client;
            first.Disconnect();
            var result = await changed.ConnectAndAuthenticateAsync("smtp.example.test", 587, "account-A", secret, authMode: mode);
            Assert.NotSame(transport, changed.Client);
            if (string.IsNullOrEmpty(secret)) Assert.False(result.IsSuccess);
            else Assert.True(result.IsSuccess);
        } finally {
            first.Dispose(); changed.Dispose();
            Smtp.ClientFactory = originalFactory;
            SmtpConnectionPool.ClearConnectionPool();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WarmedAccount_ReusesAuthenticationButAnonymousHintCannotRentIt(bool twoStep) {
        var originalFactory = Smtp.ClientFactory;
        SmtpConnectionPool.ClearConnectionPool();
        Smtp.ClientFactory = _ => new AuthenticatedClient();
        try {
            AuthenticatedClient transport;
            var first = new Smtp { UseConnectionPool = true };
            try {
                await AuthenticateAccountAsync(first, twoStep);
                transport = (AuthenticatedClient)first.Client;
            } finally { first.Dispose(); }
            var second = new Smtp { UseConnectionPool = true };
            try {
                await AuthenticateAccountAsync(second, twoStep);
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

    private static async Task AuthenticateAccountAsync(Smtp smtp, bool twoStep) {
        if (!twoStep) {
            Assert.True((await smtp.ConnectAndAuthenticateAsync("smtp.example.test", 587, "account-A", "secret")).IsSuccess);
            return;
        }
        var credential = new NetworkCredential("account-A", "secret");
        smtp.ConfigureAuthentication(credential);
        Assert.True((await smtp.ConnectAsync("smtp.example.test", 587)).Status);
        Assert.True(smtp.Authenticate(credential).Status);
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
        public override void Authenticate(Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default) {
            Authentications++; authenticated = true;
        }
        public override Task AuthenticateAsync(SaslMechanism mechanism, CancellationToken cancellationToken = default) {
            Authentications++; authenticated = true; return Task.CompletedTask;
        }
        public override void Disconnect(bool quit, CancellationToken cancellationToken = default) { connected = authenticated = false; }
    }
}
