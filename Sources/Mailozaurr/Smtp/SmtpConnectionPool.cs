namespace Mailozaurr;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
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

    private static int _maxPoolSize = 2;
    private static int _poolingEnabled = 0;

    /// <summary>Maximum number of pooled connections per server/port.</summary>
    public static int MaxPoolSize => Volatile.Read(ref _maxPoolSize);

    /// <summary>Enables or disables connection pooling.</summary>
    public static bool PoolingEnabled => Volatile.Read(ref _poolingEnabled) == 1;

    /// <summary>Sets the maximum number of pooled connections per server/port.</summary>
    /// <param name="value">Maximum number of pooled connections. Must be at least 1.</param>
    public static void SetMaxPoolSize(int value) {
        if (value < 1) {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Max pool size must be at least 1.");
        }

        Interlocked.Exchange(ref _maxPoolSize, value);
    }

    /// <summary>Enables or disables connection pooling.</summary>
    /// <param name="enabled">If true, reuses SMTP connections where possible.</param>
    public static void SetPoolingEnabled(bool enabled) {
        var newValue = enabled ? 1 : 0;
        var previous = Interlocked.Exchange(ref _poolingEnabled, newValue);
        if (previous == 1 && newValue == 0) {
            ClearConnectionPool();
        }
    }

    /// <summary>Configures pooling settings in a single call.</summary>
    /// <param name="poolingEnabled">Whether pooling should be enabled.</param>
    /// <param name="maxPoolSize">Maximum number of pooled connections per endpoint.</param>
    public static void Configure(bool poolingEnabled, int maxPoolSize) {
        if (maxPoolSize < 1) {
            throw new ArgumentOutOfRangeException(nameof(maxPoolSize), maxPoolSize, "Max pool size must be at least 1.");
        }

        SetPoolingEnabled(poolingEnabled);
        SetMaxPoolSize(maxPoolSize);
    }

    /// <summary>Number of pooled SMTP clients across all servers.</summary>
    public static int CurrentPoolSize {
        get {
            var total = 0;
            foreach (var entry in _connectionPool.Values) {
                total += entry.Count;
            }

            return total;
        }
    }

    /// <summary>Raised whenever the pool size changes.</summary>
    public static event Action<int>? PoolSizeChanged;

    private static void OnPoolSizeChanged() {
        var size = CurrentPoolSize;
        PoolSizeChanged?.Invoke(size);
    }

    private static string EncodeKeyPart(string value) {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
    }

    private static string BuildKey(string server, int port, string? identity) {
        var serverKey = EncodeKeyPart(server);
        if (string.IsNullOrWhiteSpace(identity)) {
            return $"{serverKey}:{port}";
        }
        var identityKey = EncodeKeyPart(identity);
        return $"{serverKey}:{port}:{identityKey}";
    }

    internal static ClientSmtp? TryRentClient(string server, int port, string? identity = null) {
        if (!PoolingEnabled) {
            return null;
        }

        var key = BuildKey(server, port, identity);
        if (_connectionPool.TryGetValue(key, out var entry)) {
            while (entry.Bag.TryTake(out var pooled)) {
                Interlocked.Decrement(ref entry.Count);
                OnPoolSizeChanged();
                if (pooled.IsConnected) {
                    return pooled;
                }

                pooled.Dispose();
            }
        }

        return null;
    }

    internal static void ReturnClient(string server, int port, ClientSmtp client, string? identity = null) {
        if (!PoolingEnabled) {
            client.Dispose();
            return;
        }

        if (!client.IsConnected) {
            client.Dispose();
            return;
        }

        var key = BuildKey(server, port, identity);
        var entry = _connectionPool.GetOrAdd(key, _ => new PoolEntry());
        var current = Interlocked.Increment(ref entry.Count);
        if (current > MaxPoolSize) {
            Interlocked.Decrement(ref entry.Count);
            client.Dispose();
        } else {
            entry.Bag.Add(client);
        }

        OnPoolSizeChanged();
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
        OnPoolSizeChanged();
    }

    /// <summary>Gets a snapshot of the current connection pool state.</summary>
    /// <returns>Snapshot containing all pooled connections.</returns>
    public static SmtpConnectionPoolSnapshot GetSnapshot() {
        var entries = new List<SmtpConnectionPoolEntry>();
        foreach (var kv in _connectionPool) {
            var key = kv.Key;
            var parts = key.Split(':');
            var server = parts.Length > 0 ? parts[0] : key;
            var port = 0;
            if (parts.Length > 1) {
                int.TryParse(parts[1], out port);
            }
            try {
                server = Encoding.UTF8.GetString(Convert.FromBase64String(server));
            } catch (FormatException) {
                // Keep raw server key if decoding fails.
            }
            entries.Add(new SmtpConnectionPoolEntry(server, port, kv.Value.Count));
        }

        return new SmtpConnectionPoolSnapshot(CurrentPoolSize, entries);
    }
}

/// <summary>Information about a single SMTP connection pool entry.</summary>
public sealed class SmtpConnectionPoolEntry {
    /// <summary>Server hostname for the pooled connections.</summary>
    public string Server { get; }

    /// <summary>TCP port for the pooled connections.</summary>
    public int Port { get; }

    /// <summary>Number of clients in the pool for this server and port.</summary>
    public int Count { get; }

    internal SmtpConnectionPoolEntry(string server, int port, int count) {
        Server = server;
        Port = port;
        Count = count;
    }
}

/// <summary>Represents a snapshot of the SMTP connection pool.</summary>
public sealed class SmtpConnectionPoolSnapshot {
    /// <summary>Total number of pooled SMTP clients.</summary>
    public int CurrentPoolSize { get; }

    /// <summary>Entries for each server and port combination.</summary>
    public IReadOnlyList<SmtpConnectionPoolEntry> Entries { get; }

    internal SmtpConnectionPoolSnapshot(int currentPoolSize, IReadOnlyList<SmtpConnectionPoolEntry> entries) {
        CurrentPoolSize = currentPoolSize;
        Entries = entries;
    }
}
