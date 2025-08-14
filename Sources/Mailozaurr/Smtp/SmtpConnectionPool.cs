namespace Mailozaurr;

using System.Collections.Concurrent;
using System.Threading;

/// <summary>
/// Manages SMTP connection pooling.
/// </summary>
public static class SmtpConnectionPool {
    private sealed class PoolEntry {
        public readonly ConcurrentBag<ClientSmtp> Bag = new();
        public int Count;
    }

    private static readonly ConcurrentDictionary<string, PoolEntry> _connectionPool = new();

    /// <summary>Maximum number of pooled connections per server/port.</summary>
    public static int MaxPoolSize { get; set; } = 2;

    /// <summary>Enables or disables connection pooling.</summary>
    public static bool PoolingEnabled { get; set; } = false;

    internal static ClientSmtp? TryRentClient(string server, int port) {
        if (!PoolingEnabled) {
            return null;
        }

        var key = $"{server}:{port}";
        if (_connectionPool.TryGetValue(key, out var entry)) {
            while (entry.Bag.TryTake(out var pooled)) {
                Interlocked.Decrement(ref entry.Count);
                if (pooled.IsConnected) {
                    return pooled;
                }

                pooled.Dispose();
            }
        }

        return null;
    }

    internal static void ReturnClient(string server, int port, ClientSmtp client) {
        if (!PoolingEnabled) {
            client.Dispose();
            return;
        }

        if (!client.IsConnected) {
            client.Dispose();
            return;
        }

        var key = $"{server}:{port}";
        var entry = _connectionPool.GetOrAdd(key, _ => new PoolEntry());
        var current = Interlocked.Increment(ref entry.Count);
        if (current > MaxPoolSize) {
            Interlocked.Decrement(ref entry.Count);
            client.Dispose();
        } else {
            entry.Bag.Add(client);
        }
    }

    /// <summary>Disposes all SMTP clients stored in the connection pool.</summary>
    public static void ClearConnectionPool() {
        foreach (var entry in _connectionPool.Values) {
            while (entry.Bag.TryTake(out var client)) {
                Interlocked.Decrement(ref entry.Count);
                if (!client.IsConnected) {
                    client.Dispose();
                    continue;
                }

                client.Dispose();
            }
        }

        _connectionPool.Clear();
    }
}