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
        public new bool Authenticated { get; set; }
        public override bool IsAuthenticated => Authenticated;
        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            if (ConnectCalls <= FailuresBeforeSuccess)
            {
                throw new HttpRequestException("fail");
            }
            return Task.CompletedTask;
        }
        public override Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakePop3Client : Pop3Client
    {
        public int FailuresBeforeSuccess { get; set; }
        public int ConnectCalls { get; private set; }
        public new bool Authenticated { get; set; }
        public override bool IsAuthenticated => Authenticated;
        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            if (ConnectCalls <= FailuresBeforeSuccess)
            {
                throw new HttpRequestException("fail");
            }
            return Task.CompletedTask;
        }
        public override Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task ImapConnector_RetriesUntilSuccess()
    {
        var fake = new FakeImapClient { FailuresBeforeSuccess = 2 };
        var delays = new List<int>();
        ImapConnector.ClientFactory = () => fake;
        ImapConnector.DelayAsync = d => { delays.Add(d); return Task.CompletedTask; };
        var client = await ImapConnector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            c => { ((FakeImapClient)c).Authenticated = true; return Task.CompletedTask; },
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
        ImapConnector.DelayAsync = d => { delays.Add(d); return Task.CompletedTask; };
        await Assert.ThrowsAsync<HttpRequestException>(() => ImapConnector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            c => { ((FakeImapClient)c).Authenticated = true; return Task.CompletedTask; },
            2, 10, 2));
        ImapConnector.ClientFactory = () => new ImapClient();
        ImapConnector.DelayAsync = null;
        Assert.Equal(3, fake.ConnectCalls);
        Assert.Equal(new[] { 10, 20 }, delays);
    }

    [Fact]
    public async Task Pop3Connector_RetriesUntilSuccess()
    {
        var fake = new FakePop3Client { FailuresBeforeSuccess = 1 };
        var delays = new List<int>();
        Pop3Connector.ClientFactory = () => fake;
        Pop3Connector.DelayAsync = d => { delays.Add(d); return Task.CompletedTask; };
        var client = await Pop3Connector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            c => { ((FakePop3Client)c).Authenticated = true; return Task.CompletedTask; },
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
        Pop3Connector.DelayAsync = d => { delays.Add(d); return Task.CompletedTask; };
        await Assert.ThrowsAsync<HttpRequestException>(() => Pop3Connector.ConnectAsync(
            "s", 1, SecureSocketOptions.Auto, 0, false, false,
            c => { ((FakePop3Client)c).Authenticated = true; return Task.CompletedTask; },
            2, 10, 2));
        Pop3Connector.ClientFactory = () => new Pop3Client();
        Pop3Connector.DelayAsync = null;
        Assert.Equal(3, fake.ConnectCalls);
        Assert.Equal(new[] { 10, 20 }, delays);
    }
}
