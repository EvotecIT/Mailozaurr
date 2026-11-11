using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Mailozaurr.Tests;

public sealed class ImapIdleListenerProtocolTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellationDisconnectsAnUnresponsiveIdleOrNoopPeer(bool supportsIdle) {
        await using var server = new ImapPeer(supportsIdle, blackhole: true);
        using var client = new ImapClient { Timeout = 30000 };
        await client.ConnectAsync("127.0.0.1", server.Port, SecureSocketOptions.None);
        await using var listener = new ImapIdleListener(client);
        await listener.StartAsync();
        await Within(server.FirstWait.Task);
        var stop = listener.StopAsync();
        try {
            await Within(stop);
            Assert.False(client.IsConnected);
            await Within(server.Closed.Task);
        } finally {
            if (client.IsConnected) client.Disconnect(false);
            await stop;
        }
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    public async Task FlagBatchesOnlyRescanWhenTheQueryCanDependOnFlags(int queryKind, int expectedSearches) {
        await using var server = new ImapPeer(supportsIdle: true, blackhole: false);
        using var client = new ImapClient();
        await client.ConnectAsync("127.0.0.1", server.Port, SecureSocketOptions.None);
        await using var listener = new ImapIdleListener(client, searchQuery: queryKind == 2 ? SearchQuery.Seen : queryKind == 1 ? SearchQuery.All : null);
        await listener.StartAsync();
        await Within(server.SecondWait.Task);
        Assert.Equal(expectedSearches, server.Searches);
        await listener.StopAsync();
        Assert.True(client.IsConnected);
    }

    private static async Task Within(Task task) {
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))));
        await task;
    }

    /// <summary>Controlled local peer; can ignore DONE/NOOP or deliver a flag batch.</summary>
    private sealed class ImapPeer : IAsyncDisposable {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly bool supportsIdle;
        private readonly bool blackhole;
        private readonly Task serving;
        private TcpClient? connection;
        private int searches;
        internal TaskCompletionSource<bool> FirstWait { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> SecondWait { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Searches => Volatile.Read(ref searches);
        internal int Port { get; }

        internal ImapPeer(bool supportsIdle, bool blackhole) {
            this.supportsIdle = supportsIdle;
            this.blackhole = blackhole;
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            serving = ServeAsync();
        }

        private async Task ServeAsync() {
            connection = await listener.AcceptTcpClientAsync();
            using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true };
            var capabilities = "IMAP4rev1" + (supportsIdle ? " IDLE" : string.Empty);
            await writer.WriteLineAsync("* PREAUTH [CAPABILITY " + capabilities + "] loopback");
            string? idleTag = null;
            var waits = 0;
            while (true) {
                var line = await reader.ReadLineAsync();
                if (line == null) { Closed.TrySetResult(true); return; }
                if (line == "DONE") {
                    if (!blackhole) await writer.WriteLineAsync(idleTag + " OK idle finished");
                    idleTag = null;
                    continue;
                }
                var split = line.IndexOf(' ');
                var tag = line.Substring(0, split);
                var command = line.Substring(split + 1);
                if (command.StartsWith("CAPABILITY")) {
                    await writer.WriteLineAsync("* CAPABILITY " + capabilities);
                } else if (command.StartsWith("LIST")) {
                    await writer.WriteLineAsync("* LIST (\\HasNoChildren) \"/\" \"INBOX\"");
                } else if (command.StartsWith("EXAMINE") || command.StartsWith("SELECT")) {
                    await writer.WriteLineAsync("* FLAGS (\\Seen)");
                    await writer.WriteLineAsync("* 1 EXISTS");
                    await writer.WriteLineAsync("* OK [UIDVALIDITY 1] stable");
                    await writer.WriteLineAsync("* OK [UIDNEXT 2] next");
                    await writer.WriteLineAsync(tag + " OK [READ-ONLY] selected");
                    continue;
                } else if (command.StartsWith("UID SEARCH")) {
                    Interlocked.Increment(ref searches);
                    await writer.WriteLineAsync("* SEARCH 1");
                } else if (command == "IDLE" || command == "NOOP") {
                    waits++;
                    if (command == "IDLE") {
                        idleTag = tag;
                        await writer.WriteLineAsync("+ idling");
                        if (!blackhole && waits == 1) await writer.WriteLineAsync("* 1 FETCH (UID 1 FLAGS (\\Seen))");
                    }
                    FirstWait.TrySetResult(true);
                    if (waits == 2) SecondWait.TrySetResult(true);
                    if (command == "IDLE" || blackhole) continue;
                } else if (command == "LOGOUT") {
                    await writer.WriteLineAsync("* BYE done");
                    await writer.WriteLineAsync(tag + " OK logged out");
                    return;
                } else {
                    throw new InvalidOperationException("Unexpected IMAP command: " + command);
                }
                await writer.WriteLineAsync(tag + " OK completed");
            }
        }

        public async ValueTask DisposeAsync() {
            listener.Stop();
            connection?.Dispose();
            try { await serving; }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }
    }
}
