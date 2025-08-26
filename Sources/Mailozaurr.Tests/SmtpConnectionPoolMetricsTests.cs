using System.Collections.Generic;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpConnectionPoolMetricsTests {
    private class FakeClient : ClientSmtp {
        private bool _connected = true;
        public override bool IsConnected => _connected;
        public void SetConnected(bool value) => _connected = value;
    }

    [Fact]
    public void CurrentPoolSize_TracksClients() {
        SmtpConnectionPool.SetPoolingEnabled(true);
        SmtpConnectionPool.ClearConnectionPool();

        Assert.Equal(0, SmtpConnectionPool.CurrentPoolSize);

        var client = new FakeClient();
        SmtpConnectionPool.ReturnClient("h", 25, client);
        Assert.Equal(1, SmtpConnectionPool.CurrentPoolSize);

        var rented = SmtpConnectionPool.TryRentClient("h", 25);
        Assert.NotNull(rented);
        Assert.Equal(0, SmtpConnectionPool.CurrentPoolSize);

        SmtpConnectionPool.ClearConnectionPool();
        SmtpConnectionPool.SetPoolingEnabled(false);
    }

    [Fact]
    public void PoolSizeChanged_Raised() {
        SmtpConnectionPool.SetPoolingEnabled(true);
        SmtpConnectionPool.ClearConnectionPool();

        var values = new List<int>();
        void Handler(int size) => values.Add(size);
        SmtpConnectionPool.PoolSizeChanged += Handler;

        var client = new FakeClient();
        SmtpConnectionPool.ReturnClient("h", 25, client);
        var rented = SmtpConnectionPool.TryRentClient("h", 25);
        rented?.Dispose();

        // clearing the pool should also raise the event
        SmtpConnectionPool.ClearConnectionPool();

        SmtpConnectionPool.PoolSizeChanged -= Handler;
        SmtpConnectionPool.SetPoolingEnabled(false);

        Assert.Equal(new[] { 1, 0, 0 }, values);
    }

    [Fact]
    public void GetSnapshot_ReturnsEntries() {
        SmtpConnectionPool.SetPoolingEnabled(true);
        SmtpConnectionPool.ClearConnectionPool();

        var client = new FakeClient();
        SmtpConnectionPool.ReturnClient("h", 25, client);

        var snapshot = SmtpConnectionPool.GetSnapshot();
        Assert.Equal(1, snapshot.CurrentPoolSize);
        Assert.Single(snapshot.Entries);
        var entry = snapshot.Entries[0];
        Assert.Equal("h", entry.Server);
        Assert.Equal(25, entry.Port);
        Assert.Equal(1, entry.Count);

        SmtpConnectionPool.ClearConnectionPool();
        SmtpConnectionPool.SetPoolingEnabled(false);
    }
}

