using MailKit.Security;
using System;
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

        public override void Disconnect(bool quit, CancellationToken cancellationToken = default) {
            _connected = false;
        }
    }

    private sealed class TrackingClient : ClientSmtp {
        private bool _connected = true;
        private bool _disposed;

        public bool Disposed => _disposed;

        public override bool IsConnected => _connected;

        public void SetConnected(bool value) => _connected = value;

        protected override void Dispose(bool disposing) {
            _disposed = true;
            _connected = false;
            base.Dispose(disposing);
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
    public void InstancePoolOverrideFalse_DoesNotUseGlobalPool() {
        SmtpConnectionPool.SetPoolingEnabled(true);
        SmtpConnectionPool.ClearConnectionPool();

        var fake = new FakeClient();
        Smtp.ClientFactory = _ => fake;

        var smtp1 = new Smtp { UseConnectionPool = false };
        smtp1.Connect("h", 25);
        smtp1.Disconnect();

        Smtp.ClientFactory = _ => new FakeClient();
        var smtp2 = new Smtp { UseConnectionPool = false };
        smtp2.Connect("h", 25);

        Assert.NotSame(fake, smtp2.Client);
        Assert.Equal(0, SmtpConnectionPool.CurrentPoolSize);

        smtp2.Dispose();
        Smtp.ClientFactory = _ => new ClientSmtp();
        SmtpConnectionPool.ClearConnectionPool();
        SmtpConnectionPool.SetPoolingEnabled(false);
    }

    [Fact]
    public void InstancePoolOverrideTrue_UsesPoolWhenGlobalPoolingIsDisabled() {
        SmtpConnectionPool.SetPoolingEnabled(false);
        SmtpConnectionPool.ClearConnectionPool();

        var fake = new FakeClient();
        Smtp.ClientFactory = _ => fake;

        var smtp1 = new Smtp { UseConnectionPool = true };
        smtp1.Connect("h", 25);
        smtp1.Disconnect();

        var smtp2 = new Smtp { UseConnectionPool = true };
        Smtp.ClientFactory = _ => new FakeClient();
        smtp2.Connect("h", 25);

        Assert.Same(fake, smtp2.Client);
        Assert.Equal(1, fake.ConnectCalls);

        smtp2.Dispose();
        Smtp.ClientFactory = _ => new ClientSmtp();
        SmtpConnectionPool.ClearConnectionPool();
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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetMaxPoolSize_InvalidValue_Throws(int value) {
        var original = SmtpConnectionPool.MaxPoolSize;

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => SmtpConnectionPool.SetMaxPoolSize(value));
        Assert.Equal("value", ex.ParamName);
        Assert.Equal(original, SmtpConnectionPool.MaxPoolSize);
    }

    [Fact]
    public void SetMaxPoolSize_ValidValue_UpdatesProperty() {
        var original = SmtpConnectionPool.MaxPoolSize;
        var expected = original + 1;

        try {
            SmtpConnectionPool.SetMaxPoolSize(expected);
            Assert.Equal(expected, SmtpConnectionPool.MaxPoolSize);
        } finally {
            SmtpConnectionPool.SetMaxPoolSize(original);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Configure_InvalidMaxPoolSize_Throws(int value) {
        var originalEnabled = SmtpConnectionPool.PoolingEnabled;
        var originalMax = SmtpConnectionPool.MaxPoolSize;

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => SmtpConnectionPool.Configure(true, value));
        Assert.Equal("maxPoolSize", ex.ParamName);
        Assert.Equal(originalEnabled, SmtpConnectionPool.PoolingEnabled);
        Assert.Equal(originalMax, SmtpConnectionPool.MaxPoolSize);
    }

    [Fact]
    public void Configure_ValidMaxPoolSize_UpdatesProperty() {
        var originalEnabled = SmtpConnectionPool.PoolingEnabled;
        var originalMax = SmtpConnectionPool.MaxPoolSize;
        var expected = originalMax + 1;

        try {
            SmtpConnectionPool.Configure(true, expected);
            Assert.True(SmtpConnectionPool.PoolingEnabled);
            Assert.Equal(expected, SmtpConnectionPool.MaxPoolSize);
        } finally {
            SmtpConnectionPool.Configure(originalEnabled, originalMax);
        }
    }

    [Fact]
    public void Configure_DisablingPooling_ClearsCachedClients() {
        var originalEnabled = SmtpConnectionPool.PoolingEnabled;
        var originalMax = SmtpConnectionPool.MaxPoolSize;

        var client = new TrackingClient();
        client.SetConnected(true);

        try {
            SmtpConnectionPool.Configure(true, Math.Max(2, originalMax));
            SmtpConnectionPool.ClearConnectionPool();

            SmtpConnectionPool.ReturnClient("h", 25, client);
            Assert.Equal(1, SmtpConnectionPool.CurrentPoolSize);

            SmtpConnectionPool.Configure(false, originalMax);

            Assert.True(client.Disposed);
            Assert.Equal(0, SmtpConnectionPool.CurrentPoolSize);
            Assert.False(SmtpConnectionPool.PoolingEnabled);
        } finally {
            SmtpConnectionPool.ClearConnectionPool();
            SmtpConnectionPool.Configure(originalEnabled, originalMax);
        }
    }
}