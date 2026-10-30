using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Mailozaurr.Tests;

/// <summary>Local protocol peer; never relays or delivers mail outside the test process.</summary>
internal sealed class LoopbackSmtpServer : IAsyncDisposable {
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource shutdown = new();
    private readonly ConcurrentBag<TcpClient> clients = new();
    private readonly List<Task> handlers = new();
    private readonly Task accepting;
    private readonly int recipientCode;
    private readonly int contentCode;
    private readonly bool silentGreeting;
    private readonly bool dropAfterData;
    private readonly int? authenticationCode;
    private readonly bool advertiseDsn;
    private int authentications;
    internal ConcurrentQueue<string> MessageIds { get; } = new();
    internal ConcurrentQueue<string> MailFromCommands { get; } = new();
    internal ConcurrentQueue<string> RecipientCommands { get; } = new();
    private readonly TaskCompletionSource<bool> connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int connections;
    private int recipients;
    private int contentSubmissions;
    private int accepted;

    internal int Port { get; }
    internal int Connections => Volatile.Read(ref connections);
    internal int Recipients => Volatile.Read(ref recipients);
    internal int ContentSubmissions => Volatile.Read(ref contentSubmissions);
    internal int Accepted => Volatile.Read(ref accepted);
    internal Task Connected => connected.Task;
    internal int Authentications => Volatile.Read(ref authentications);

    internal LoopbackSmtpServer(int recipientCode = 250, int contentCode = 250, bool silentGreeting = false, bool dropAfterData = false, int? authenticationCode = null, bool advertiseDsn = false) {
        this.recipientCode = recipientCode;
        this.contentCode = contentCode;
        this.silentGreeting = silentGreeting;
        this.dropAfterData = dropAfterData;
        this.authenticationCode = authenticationCode;
        this.advertiseDsn = advertiseDsn;
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        accepting = AcceptAsync();
    }

    private async Task AcceptAsync() {
        try {
            while (!shutdown.IsCancellationRequested) {
                var client = await listener.AcceptTcpClientAsync();
                clients.Add(client);
                Interlocked.Increment(ref connections);
                connected.TrySetResult(true);
                handlers.Add(HandleAsync(client));
            }
        } catch (ObjectDisposedException) when (shutdown.IsCancellationRequested) {
        } catch (SocketException) when (shutdown.IsCancellationRequested) {
        }
    }

    private async Task HandleAsync(TcpClient client) {
        using (client) {
            try {
                if (silentGreeting) {
                    await Task.Delay(Timeout.Infinite, shutdown.Token);
                    return;
                }
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true };
                await writer.WriteLineAsync("220 loopback ESMTP");
                var authenticated = false;
                while (!shutdown.IsCancellationRequested) {
                    var line = await reader.ReadLineAsync();
                    if (line == null) return;
                    var verb = line.Split(' ')[0].ToUpperInvariant();
                    switch (verb) {
                        case "EHLO":
                            if (authenticationCode.HasValue || advertiseDsn) {
                                await writer.WriteLineAsync("250-loopback");
                                if (authenticationCode.HasValue) {
                                    await writer.WriteLineAsync(advertiseDsn ? "250-AUTH PLAIN" : "250 AUTH PLAIN");
                                }
                                if (advertiseDsn) await writer.WriteLineAsync("250 DSN");
                            } else {
                                await writer.WriteLineAsync("250 loopback");
                            }
                            break;
                        case "HELO":
                            await writer.WriteLineAsync("250 loopback");
                            break;
                        case "MAIL":
                            MailFromCommands.Enqueue(line);
                            if (authenticationCode.HasValue && !authenticated) {
                                await writer.WriteLineAsync("530 authentication required");
                            } else {
                                await writer.WriteLineAsync("250 OK");
                            }
                            break;
                        case "AUTH":
                            if (!authenticationCode.HasValue || !line.StartsWith("AUTH PLAIN", StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("Expected advertised AUTH PLAIN mechanism.");
                            if (line.Split(' ').Length == 2) {
                                await writer.WriteLineAsync("334 ");
                                if (await reader.ReadLineAsync() == null) return;
                            }
                            Interlocked.Increment(ref authentications);
                            authenticated = authenticationCode == 235;
                            await writer.WriteLineAsync($"{authenticationCode} authentication response");
                            break;
                        case "RSET":
                        case "NOOP":
                            await writer.WriteLineAsync("250 OK");
                            break;
                        case "RCPT":
                            RecipientCommands.Enqueue(line);
                            Interlocked.Increment(ref recipients);
                            await writer.WriteLineAsync($"{recipientCode} recipient response");
                            break;
                        case "DATA":
                            await writer.WriteLineAsync("354 end with dot");
                            var data = new StringBuilder();
                            do {
                                line = await reader.ReadLineAsync();
                                if (line == null) return;
                                if (line != ".") data.AppendLine(line);
                            } while (line != ".");
                            using (var mime = MimeKit.MimeMessage.Load(new MemoryStream(Encoding.ASCII.GetBytes(data.ToString())))) {
                                MessageIds.Enqueue(mime.MessageId ?? string.Empty);
                            }
                            Interlocked.Increment(ref contentSubmissions);
                            if (dropAfterData) return;
                            if (contentCode == 250) Interlocked.Increment(ref accepted);
                            await writer.WriteLineAsync($"{contentCode} content response");
                            break;
                        case "QUIT":
                            await writer.WriteLineAsync("221 bye");
                            return;
                        default:
                            throw new InvalidOperationException($"Unexpected SMTP command: {verb}");
                    }
                }
            } catch (OperationCanceledException) when (shutdown.IsCancellationRequested) {
            } catch (IOException) {
                // A client may disconnect after rejection, timeout or cancellation.
            } catch (ObjectDisposedException) when (shutdown.IsCancellationRequested) {
            }
        }
    }

    public async ValueTask DisposeAsync() {
        shutdown.Cancel();
        listener.Stop();
        try {
            await accepting;
        } finally {
            foreach (var client in clients) client.Dispose();
            try { await Task.WhenAll(handlers); }
            finally { shutdown.Dispose(); }
        }
    }
}
