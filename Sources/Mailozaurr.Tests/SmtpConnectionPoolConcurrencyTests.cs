using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpConnectionPoolConcurrencyTests {
    private class FakeClient : ClientSmtp {
        private bool _connected = true;
        public override bool IsConnected => _connected;
        public void SetConnected(bool value) => _connected = value;
    }

    [Fact]
    public void ReturnClient_ConcurrentCallsRespectMaxPoolSize() {
        SmtpConnectionPool.Configure(true, 2);
        SmtpConnectionPool.ClearConnectionPool();

        var clients = Enumerable.Range(0, 20).Select(_ => new FakeClient()).ToArray();
        Parallel.ForEach(clients, c => SmtpConnectionPool.ReturnClient("h", 25, c));

        var rented = 0;
        ClientSmtp? client;
        while ((client = SmtpConnectionPool.TryRentClient("h", 25)) != null) {
            rented++;
            client.Dispose();
        }

        Assert.Equal(SmtpConnectionPool.MaxPoolSize, rented);

        SmtpConnectionPool.ClearConnectionPool();
        SmtpConnectionPool.SetPoolingEnabled(false);
    }

    [Fact]
    public void TryRentClient_ConcurrentCallsEmptyPool() {
        SmtpConnectionPool.Configure(true, 5);
        SmtpConnectionPool.ClearConnectionPool();

        foreach (var _ in Enumerable.Range(0, SmtpConnectionPool.MaxPoolSize)) {
            SmtpConnectionPool.ReturnClient("h", 25, new FakeClient());
        }

        var rented = 0;
        Parallel.For(0, SmtpConnectionPool.MaxPoolSize, _ => {
            var client = SmtpConnectionPool.TryRentClient("h", 25);
            if (client != null) {
                Interlocked.Increment(ref rented);
                client.Dispose();
            }
        });

        Assert.Equal(SmtpConnectionPool.MaxPoolSize, rented);
        Assert.Null(SmtpConnectionPool.TryRentClient("h", 25));

        SmtpConnectionPool.ClearConnectionPool();
        SmtpConnectionPool.SetPoolingEnabled(false);
    }
}