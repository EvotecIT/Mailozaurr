using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Security;
using Xunit;

namespace Mailozaurr.Tests;

public class ConnectorTests
{
    private class FakeImapClient : ImapClient
    {
        public int FailuresBeforeSuccess { get; set; }
        public int ConnectCalls { get; private set; }
        public string? LastHost { get; private set; }
        public int LastPort { get; private set; }
        public SecureSocketOptions LastSecureSocketOptions { get; private set; }
        public new bool Authenticated { get; set; }
        public override bool IsAuthenticated => Authenticated;
        private bool _connected;
        private int _timeout;
        public override bool IsConnected => _connected;
        public override int Timeout { get => _timeout; set => _timeout = value; }
        public bool Disposed { get; private set; }
        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            LastHost = host;
            LastPort = port;
            LastSecureSocketOptions = options;
            if (ConnectCalls <= FailuresBeforeSuccess)
            {
                throw new HttpRequestException("fail");
            }
            _connected = true;
            return Task.CompletedTask;
        }
        public override Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
        {
            _connected = false;
            return Task.CompletedTask;
        }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private class FakePop3Client : Pop3Client
    {
        public int FailuresBeforeSuccess { get; set; }
        public int ConnectCalls { get; private set; }
        public new bool Authenticated { get; set; }
        public override bool IsAuthenticated => Authenticated;
        private bool _connected;
        private int _timeout;
        public override bool IsConnected => _connected;
        public override int Timeout { get => _timeout; set => _timeout = value; }
        public bool Disposed { get; private set; }
        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            if (ConnectCalls <= FailuresBeforeSuccess)
            {
                throw new HttpRequestException("fail");
            }
            _connected = true;
            return Task.CompletedTask;
        }
        public override Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
        {
            _connected = false;
            return Task.CompletedTask;
        }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private class FakeImapDisconnectFailClient : FakeImapClient
    {
        public override Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
            => Task.FromException(new InvalidOperationException("disconnect"));
    }

    private class FakePop3DisconnectFailClient : FakePop3Client
    {
        public override Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
            => Task.FromException(new InvalidOperationException("disconnect"));
    }

    [Fact]
    public async Task ImapConnector_RetriesUntilSuccess()
    {
        var fake = new FakeImapClient { FailuresBeforeSuccess = 2 };
        var delays = new List<int>();
        ImapConnector.ClientFactory = () => fake;
        ImapConnector.DelayAsync = (d, ct) => { delays.Add(d); return Task.CompletedTask; };
        var client = await ImapConnector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            (c, ct) => { ((FakeImapClient)c).Authenticated = true; return Task.CompletedTask; },
            3, 10, 2);
        ImapConnector.ClientFactory = () => new ImapClient();
        ImapConnector.DelayAsync = null;
        Assert.Same(fake, client);
        Assert.Equal(3, fake.ConnectCalls);
        Assert.Equal(new[] { 10, 20 }, delays);
    }

    [Fact]
    public async Task ImapConnector_ThrowsAfterRetries()
    {
        var fake = new FakeImapClient { FailuresBeforeSuccess = 5 };
        var delays = new List<int>();
        ImapConnector.ClientFactory = () => fake;
        ImapConnector.DelayAsync = (d, ct) => { delays.Add(d); return Task.CompletedTask; };
        await Assert.ThrowsAsync<HttpRequestException>(() => ImapConnector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            (c, ct) => { ((FakeImapClient)c).Authenticated = true; return Task.CompletedTask; },
            2, 10, 2));
        ImapConnector.ClientFactory = () => new ImapClient();
        ImapConnector.DelayAsync = null;
        Assert.Equal(3, fake.ConnectCalls);
        Assert.Equal(new[] { 10, 20 }, delays);
    }

    [Fact]
    public async Task ImapConnector_RequestOverload_UsesRequestSettings()
    {
        var fake = new FakeImapClient();
        ImapConnector.ClientFactory = () => fake;
        var request = new ImapConnectionRequest(
            "imap.example.test",
            1993,
            SecureSocketOptions.SslOnConnect,
            timeout: 4321,
            skipCertificateRevocation: true,
            skipCertificateValidation: true,
            retryCount: 0,
            retryDelayMilliseconds: 10,
            retryDelayBackoff: 2.0);

        var client = await ImapConnector.ConnectAsync(
            request,
            (c, ct) => { ((FakeImapClient)c).Authenticated = true; return Task.CompletedTask; });

        ImapConnector.ClientFactory = () => new ImapClient();

        Assert.Same(fake, client);
        Assert.Equal("imap.example.test", fake.LastHost);
        Assert.Equal(1993, fake.LastPort);
        Assert.Equal(SecureSocketOptions.SslOnConnect, fake.LastSecureSocketOptions);
        Assert.Equal(4321, fake.Timeout);
    }

    [Fact]
    public async Task ImapConnector_RequestOverload_ValidatesArguments()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ImapConnector.ConnectAsync(
                null!,
                (_, _) => Task.CompletedTask));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ImapConnector.ConnectAsync(
                new ImapConnectionRequest("imap.example.test", 993),
                null!));
    }

    [Fact]
    public async Task Pop3Connector_RetriesUntilSuccess()
    {
        var fake = new FakePop3Client { FailuresBeforeSuccess = 1 };
        var delays = new List<int>();
        Pop3Connector.ClientFactory = () => fake;
        Pop3Connector.DelayAsync = (d, ct) => { delays.Add(d); return Task.CompletedTask; };
        var client = await Pop3Connector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            (c, ct) => { ((FakePop3Client)c).Authenticated = true; return Task.CompletedTask; },
            2, 10, 2);
        Pop3Connector.ClientFactory = () => new Pop3Client();
        Pop3Connector.DelayAsync = null;
        Assert.Same(fake, client);
        Assert.Equal(2, fake.ConnectCalls);
        Assert.Equal(new[] { 10 }, delays);
    }

    [Fact]
    public async Task Pop3Connector_ThrowsAfterRetries()
    {
        var fake = new FakePop3Client { FailuresBeforeSuccess = 4 };
        var delays = new List<int>();
        Pop3Connector.ClientFactory = () => fake;
        Pop3Connector.DelayAsync = (d, ct) => { delays.Add(d); return Task.CompletedTask; };
        await Assert.ThrowsAsync<HttpRequestException>(() => Pop3Connector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            (c, ct) => { ((FakePop3Client)c).Authenticated = true; return Task.CompletedTask; },
            2, 10, 2));
        Pop3Connector.ClientFactory = () => new Pop3Client();
        Pop3Connector.DelayAsync = null;
        Assert.Equal(3, fake.ConnectCalls);
        Assert.Equal(new[] { 10, 20 }, delays);
    }

    [Fact]
    public async Task ImapConnector_NoRetriesThrowsOriginalException()
    {
        var fake = new FakeImapClient { FailuresBeforeSuccess = 1 };
        ImapConnector.ClientFactory = () => fake;
        await Assert.ThrowsAsync<HttpRequestException>(() => ImapConnector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            (c, ct) => { ((FakeImapClient)c).Authenticated = true; return Task.CompletedTask; },
            0, 10, 2));
        ImapConnector.ClientFactory = () => new ImapClient();
        Assert.Equal(1, fake.ConnectCalls);
    }

    [Fact]
    public async Task Pop3Connector_NoRetriesThrowsOriginalException()
    {
        var fake = new FakePop3Client { FailuresBeforeSuccess = 1 };
        Pop3Connector.ClientFactory = () => fake;
        await Assert.ThrowsAsync<HttpRequestException>(() => Pop3Connector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            (c, ct) => { ((FakePop3Client)c).Authenticated = true; return Task.CompletedTask; },
            0, 10, 2));
        Pop3Connector.ClientFactory = () => new Pop3Client();
        Assert.Equal(1, fake.ConnectCalls);
    }

    [Fact]
    public async Task ImapConnector_CancellationStopsRetries()
    {
        var fake = new FakeImapClient { FailuresBeforeSuccess = 5 };
        var cts = new CancellationTokenSource();
        ImapConnector.ClientFactory = () => fake;
        ImapConnector.DelayAsync = (d, ct) => { cts.Cancel(); return Task.Delay(d, ct); };
        await Assert.ThrowsAsync<TaskCanceledException>(() => ImapConnector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            (c, ct) => { ((FakeImapClient)c).Authenticated = true; return Task.CompletedTask; },
            3, 10, 2, cts.Token));
        ImapConnector.ClientFactory = () => new ImapClient();
        ImapConnector.DelayAsync = null;
        Assert.Equal(1, fake.ConnectCalls);
    }

    [Fact]
    public async Task Pop3Connector_CancellationStopsRetries()
    {
        var fake = new FakePop3Client { FailuresBeforeSuccess = 5 };
        var cts = new CancellationTokenSource();
        Pop3Connector.ClientFactory = () => fake;
        Pop3Connector.DelayAsync = (d, ct) => { cts.Cancel(); return Task.Delay(d, ct); };
        await Assert.ThrowsAsync<TaskCanceledException>(() => Pop3Connector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            (c, ct) => { ((FakePop3Client)c).Authenticated = true; return Task.CompletedTask; },
            3, 10, 2, cts.Token));
        Pop3Connector.ClientFactory = () => new Pop3Client();
        Pop3Connector.DelayAsync = null;
        Assert.Equal(1, fake.ConnectCalls);
    }

    [Fact]
    public async Task ImapConnector_LogsDisconnectException()
    {
        var fake = new FakeImapDisconnectFailClient();
        ImapConnector.ClientFactory = () => fake;
        var messages = new List<string>();
        void Handler(object? _, LogEventArgs e) => messages.Add(e.Message);
        LoggingMessages.Logger.OnWarningMessage += Handler;

        await Assert.ThrowsAsync<InvalidOperationException>(() => ImapConnector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            (_, _) => throw new InvalidOperationException("auth"),
            0, 0, 1));

        LoggingMessages.Logger.OnWarningMessage -= Handler;
        ImapConnector.ClientFactory = () => new ImapClient();
        Assert.Contains(messages, static m => m.Contains("disconnect"));
    }

    [Fact]
    public async Task ImapConnector_DisposesClientBeforeRetry()
    {
        var first = new FakeImapClient { FailuresBeforeSuccess = 1 };
        var second = new FakeImapClient();
        var call = 0;
        ImapConnector.ClientFactory = () =>
        {
            call++;
            if (call == 2)
            {
                Assert.True(first.Disposed);
                return second;
            }
            return first;
        };
        var client = await ImapConnector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            (c, ct) => { ((FakeImapClient)c).Authenticated = true; return Task.CompletedTask; },
            1, 0, 1);
        ImapConnector.ClientFactory = () => new ImapClient();
        Assert.Same(second, client);
    }

    [Fact]
    public async Task ImapConnector_DisposesClientAfterFinalFailure()
    {
        var fake = new FakeImapClient { FailuresBeforeSuccess = 1 };
        ImapConnector.ClientFactory = () => fake;
        await Assert.ThrowsAsync<HttpRequestException>(() => ImapConnector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            (c, ct) => { ((FakeImapClient)c).Authenticated = true; return Task.CompletedTask; },
            0, 0, 1));
        ImapConnector.ClientFactory = () => new ImapClient();
        Assert.True(fake.Disposed);
    }

    [Fact]
    public async Task Pop3Connector_LogsDisconnectException()
    {
        var fake = new FakePop3DisconnectFailClient();
        Pop3Connector.ClientFactory = () => fake;
        var messages = new List<string>();
        void Handler(object? _, LogEventArgs e) => messages.Add(e.Message);
        LoggingMessages.Logger.OnWarningMessage += Handler;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Pop3Connector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            (_, _) => throw new InvalidOperationException("auth"),
            0, 0, 1));

        LoggingMessages.Logger.OnWarningMessage -= Handler;
        Pop3Connector.ClientFactory = () => new Pop3Client();
        Assert.Contains(messages, static m => m.Contains("disconnect"));
    }

    [Fact]
    public async Task Pop3Connector_DisposesClientBeforeRetry()
    {
        var first = new FakePop3Client { FailuresBeforeSuccess = 1 };
        var second = new FakePop3Client();
        var call = 0;
        Pop3Connector.ClientFactory = () =>
        {
            call++;
            if (call == 2)
            {
                Assert.True(first.Disposed);
                return second;
            }
            return first;
        };
        var client = await Pop3Connector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            (c, ct) => { ((FakePop3Client)c).Authenticated = true; return Task.CompletedTask; },
            1, 0, 1);
        Pop3Connector.ClientFactory = () => new Pop3Client();
        Assert.Same(second, client);
    }

    [Fact]
    public async Task Pop3Connector_DisposesClientAfterFinalFailure()
    {
        var fake = new FakePop3Client { FailuresBeforeSuccess = 1 };
        Pop3Connector.ClientFactory = () => fake;
        await Assert.ThrowsAsync<HttpRequestException>(() => Pop3Connector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            (c, ct) => { ((FakePop3Client)c).Authenticated = true; return Task.CompletedTask; },
            0, 0, 1));
        Pop3Connector.ClientFactory = () => new Pop3Client();
        Assert.True(fake.Disposed);
    }
}
