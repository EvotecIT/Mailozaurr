namespace Mailozaurr;

using System.Collections.Concurrent;

/// <summary>
/// Manages SMTP connection pooling.
/// </summary>
public static class SmtpConnectionPool {
    private static readonly ConcurrentDictionary<string, ConcurrentBag<ClientSmtp>> _connectionPool = new();

    /// <summary>Maximum number of pooled connections per server/port.</summary>
    public static int MaxPoolSize { get; set; } = 2;

    /// <summary>Enables or disables connection pooling.</summary>
    public static bool PoolingEnabled { get; set; } = false;

    internal static ClientSmtp? TryRentClient(string server, int port) {
        if (!PoolingEnabled) {
            return null;
        }

        var key = $"{server}:{port}";
        if (_connectionPool.TryGetValue(key, out var bag)) {
            while (bag.TryTake(out var pooled)) {
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
        var bag = _connectionPool.GetOrAdd(key, _ => new ConcurrentBag<ClientSmtp>());
        if (bag.Count >= MaxPoolSize) {
            client.Dispose();
        } else {
            bag.Add(client);
        }
    }

    /// <summary>Disposes all SMTP clients stored in the connection pool.</summary>
    public static void ClearConnectionPool() {
        foreach (var bag in _connectionPool.Values) {
            while (bag.TryTake(out var client)) {
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