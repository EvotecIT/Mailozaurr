using Mailozaurr.Definitions;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>Owns serialized multi-host delivery, reusable connections and per-host cooldowns.</summary>
/// <remarks>Equivalent request values reuse connections. Changed transport or authentication settings release old connections.
/// Custom authentication callbacks describe additional identity and mechanism settings through AuthenticationContext.
/// Callback object identity is not compared.
/// Transport loss during sending is reported as unknown delivery and is never automatically resent.</remarks>
public sealed class SmtpDeliverySession : IDisposable {
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly List<HostState> hosts = new();
    private readonly Random random = new();
    private static readonly byte[] IdentityKey = CreateIdentityKey();
    private string? transportIdentity;
    private int nextHost;
    private bool disposed;

    private sealed class HostState {
        internal readonly SmtpEndpoint Endpoint;
        internal Smtp? Connection;
        internal DateTimeOffset OpenUntil;
        internal HostState(SmtpEndpoint endpoint) => Endpoint = endpoint;
    }

    /// <summary>Sends an existing MIME message with a single global attempt budget and the request's retry backoff.</summary>
    /// <remarks>Connect delegates are rejected because they can override host selection. Use the request's authentication
    /// delegates for custom authentication; they must propagate typed failures under ErrorAction.Stop.</remarks>
    public Task<SmtpResult> SendAsync(MimeMessage message, SmtpSessionRequest request,
        SmtpDeliveryOptions options, CancellationToken ct = default) =>
        SendAsync(message, request, options, null, null, ct);

    /// <summary>Sends with per-attempt configuration and optional invocation-owned transport creation.</summary>
    /// <param name="message">Already composed MIME message; retries preserve its identifier.</param>
    /// <param name="request">Transport and authentication values.</param>
    /// <param name="options">Endpoint selection and retry policy.</param>
    /// <param name="configureTransport">Runs before every host attempt, including reused connections, to set logging,
    /// sent-log persistence and delivery notifications. Must not change transport security or authenticate.</param>
    /// <param name="createTransport">Creates a fresh, disconnected SMTP wrapper for each newly attempted host.
    /// Ownership transfers to this session. When supplied, connections are released at the end of this send,
    /// preventing invocation-specific protocol loggers from leaking into later sends. Never return a borrowed wrapper.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<SmtpResult> SendAsync(MimeMessage message, SmtpSessionRequest request,
        SmtpDeliveryOptions options, Action<Smtp>? configureTransport, Func<Smtp>? createTransport = null,
        CancellationToken ct = default) {
        if (message == null) throw new ArgumentNullException(nameof(message));
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (options == null) throw new ArgumentNullException(nameof(options));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try {
            if (disposed) throw new ObjectDisposedException(nameof(SmtpDeliverySession));
            ct.ThrowIfCancellationRequested();
            Bind(request, options);
            if (createTransport != null) foreach (var existing in hosts) DropConnection(existing);
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(message.MessageId)) message.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId();
            var attempts = new List<SmtpDeliveryAttempt>();
            var start = options.Strategy == SmtpServerStrategy.RoundRobin ? nextHost :
                options.Strategy == SmtpServerStrategy.Random ? random.Next(hosts.Count) : 0;
            nextHost = (start + 1) % hosts.Count;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            SmtpResult? last = null;
            for (var number = 1; number <= options.MaxAttempts; number++) {
                ct.ThrowIfCancellationRequested();
                var index = SelectHost(start);
                if (index < 0) {
                    if (last == null) {
                        last = Failure(message, string.Empty, 0, "All SMTP hosts are in cooldown.", true, false);
                    } else {
                        last.Error = string.IsNullOrEmpty(last.Error) ? "All SMTP hosts are in cooldown." :
                            last.Error + " All SMTP hosts are in cooldown.";
                    }
                    break;
                }
                var host = hosts[index];
                var sending = false;
                var transportStarted = false;
                var connecting = false;
                try {
                    var smtp = host.Connection ??= createTransport == null ? new Smtp() :
                        createTransport() ?? throw new InvalidOperationException("Transport factory returned null.");
                    if (createTransport != null && smtp.Client.IsConnected)
                        throw new InvalidOperationException("Transport factories must return disconnected, owned SMTP wrappers.");
                    configureTransport?.Invoke(smtp);
                    ConfigureConnection(smtp, request);
                    smtp.Message = message;
                    smtp.Subject = message.Subject ?? string.Empty;
                    smtp.From = (object?)message.Sender ?? message.From.Mailboxes.FirstOrDefault() ?? (object)string.Empty;
                    smtp.To = message.To.Mailboxes.Cast<object>().ToArray();
                    smtp.Cc = message.Cc.Mailboxes.Cast<object>().ToArray();
                    smtp.Bcc = message.Bcc.Mailboxes.Cast<object>().ToArray();
                    if (!smtp.Client.IsConnected) {
                        transportStarted = true;
                        connecting = true;
                        var connected = await smtp.ConnectAsync(host.Endpoint.Server, host.Endpoint.Port,
                            host.Endpoint.SecureSocketOptions!.Value, false, ct).ConfigureAwait(false);
                        connecting = false;
                        if (!connected.Status) {
                            last = connected;
                        } else if (request.Authenticate) {
                            last = await AuthenticateAsync(smtp, request, ct).ConfigureAwait(false);
                        } else {
                            last = connected;
                        }
                        ct.ThrowIfCancellationRequested();
                    } else {
                        last = null;
                    }
                    if (last == null || last.Status) {
                        smtp.ErrorAction = ActionPreference.Stop;
                        smtp.RetryCount = 0;
                        smtp.RetryAlways = false;
                        smtp.UseConnectionPool = false;
                        transportStarted = true;
                        sending = true;
                        last = await smtp.SendAsync(ct).ConfigureAwait(false);
                    }
                    last.Server = host.Endpoint.Server;
                    last.Port = host.Endpoint.Port;
                    last.MessageId = message.MessageId;
                    if (last.DeliveryAccepted || last.Status) {
                        host.OpenUntil = default;
                        AddAttempt(attempts, host, number, last);
                        return Finish(last, attempts, watch.Elapsed);
                    }
                    // A callback returning false provides no typed transient evidence: fail closed.
                    last.IsTransient = false;
                } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                    DropConnection(host);
                    throw;
                } catch (Exception exception) {
                    if (ct.IsCancellationRequested) {
                        DropConnection(host);
                        ct.ThrowIfCancellationRequested();
                    }
                    // Required STARTTLS on a host without that capability fails during Connect, before any send.
                    // The same exception from authentication/configuration is permanent.
                    var transient = (connecting && exception is NotSupportedException) ||
                        (transportStarted && IsTransient(exception));
                    var unknown = sending && transient && exception is not SmtpCommandException;
                    last = Failure(message, host.Endpoint.Server, host.Endpoint.Port, exception.Message, transient, unknown);
                }
                AddAttempt(attempts, host, number, last!);
                DropConnection(host);
                if (last!.IsTransient) host.OpenUntil = DateTimeOffset.UtcNow.AddMinutes(options.CircuitBreakMinutes);
                if (!last.IsTransient || last.DeliveryUnknown) return Finish(last, attempts, watch.Elapsed);
                start = (index + 1) % hosts.Count;
                if (number < options.MaxAttempts && SelectHost(start) >= 0) {
                    var delay = RetryDelayCalculator.Calculate(request.RetryDelayMilliseconds, request.RetryDelayBackoff,
                        number - 1, request.MaxDelayMilliseconds, request.JitterMilliseconds);
                    if (delay > TimeSpan.Zero) await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }
            return Finish(last!, attempts, watch.Elapsed);
        } finally {
            if (createTransport != null) foreach (var host in hosts) DropConnection(host);
            gate.Release();
        }
    }

    private void Bind(SmtpSessionRequest request, SmtpDeliveryOptions options) {
        if (request.ConnectAsync != null || request.ConnectWithCancellationAsync != null)
            throw new ArgumentException("Connect callbacks cannot override multi-host selection.", nameof(request));
        var endpoints = options.ResolveEndpoints((request.SecurityMode?.ToMailKit() ?? request.SecureSocketOptions).ResolveEffective(request.UseSsl));
        var identity = GetTransportIdentity(request, endpoints);
        if (string.Equals(transportIdentity, identity, StringComparison.Ordinal)) return;
        foreach (var host in hosts) DropConnection(host);
        hosts.Clear();
        foreach (var endpoint in endpoints) hosts.Add(new HostState(endpoint));
        transportIdentity = identity;
        nextHost = 0;
    }

    private static byte[] CreateIdentityKey() {
        var key = new byte[32];
        using var random = System.Security.Cryptography.RandomNumberGenerator.Create();
        random.GetBytes(key);
        return key;
    }

    private static string GetTransportIdentity(SmtpSessionRequest request, SmtpEndpoint[] endpoints) {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) {
            writer.Write(request.UserName ?? string.Empty);
            writer.Write(request.Password ?? string.Empty);
            writer.Write((int)request.AuthMode);
            writer.Write(request.Authenticate);
            writer.Write(request.AuthenticationContext ?? string.Empty);
            writer.Write(request.AuthenticateWithCancellationAsync != null || request.AuthenticateAsync != null);
            writer.Write(request.TimeoutMs);
            writer.Write(request.SkipCertificateValidation);
            writer.Write(request.SkipCertificateRevocation);
            writer.Write(request.DryRun);
            foreach (var endpoint in endpoints) {
                writer.Write(endpoint.Server.ToUpperInvariant());
                writer.Write(endpoint.Port);
                writer.Write((int)endpoint.SecureSocketOptions!.Value);
            }
        }
        var bytes = stream.ToArray();
        try {
            using var hash = new System.Security.Cryptography.HMACSHA256(IdentityKey);
            return Convert.ToBase64String(hash.ComputeHash(bytes));
        } finally {
            Array.Clear(bytes, 0, bytes.Length);
            Array.Clear(stream.GetBuffer(), 0, (int)stream.Length);
        }
    }

    private static void ConfigureConnection(Smtp smtp, SmtpSessionRequest request) {
        smtp.ErrorAction = ActionPreference.Stop;
        smtp.RetryCount = 0;
        smtp.RetryAlways = false;
        smtp.UseConnectionPool = false;
        smtp.Timeout = request.TimeoutMs;
        smtp.SkipCertificateValidation = request.SkipCertificateValidation;
        smtp.CheckCertificateRevocation = !request.SkipCertificateRevocation;
        smtp.DryRun = request.DryRun;
        smtp.SuppressDiagnostics = smtp.LogCollector == null && smtp.Logging == null;
    }

    private static Task<SmtpResult> AuthenticateAsync(Smtp smtp, SmtpSessionRequest request, CancellationToken ct) {
        if (request.AuthenticateWithCancellationAsync != null) return request.AuthenticateWithCancellationAsync(smtp, ct);
        if (request.AuthenticateAsync != null) return request.AuthenticateAsync(smtp);
        return smtp.AuthenticateAsync(new System.Net.NetworkCredential(request.UserName, request.Password),
            request.AuthMode == ProtocolAuthMode.OAuth2, ct);
    }

    private int SelectHost(int start) {
        for (var offset = 0; offset < hosts.Count; offset++) {
            var index = (start + offset) % hosts.Count;
            if (hosts[index].OpenUntil <= DateTimeOffset.UtcNow) return index;
        }
        return -1;
    }

    private static bool IsTransient(Exception exception) {
        // AuthenticationException wraps the server's command failure; SMTP status takes precedence over wrapper type.
        for (Exception? current = exception; current != null; current = current.InnerException) {
            if (current is SmtpCommandException command)
                return (int)command.StatusCode >= 400 && (int)command.StatusCode < 500;
        }
        if (exception is MailKit.Security.AuthenticationException authentication) {
            // Explicit SASL authentication preserves SMTP status only in MailKit's invariant "ddd: " prefix.
            // Recognize only a complete 4xx prefix; 5xx and unrecognized authentication failures remain permanent.
            var message = authentication.Message;
            return message.Length >= 5 && message[0] == '4' &&
                message[1] >= '0' && message[1] <= '9' &&
                message[2] >= '0' && message[2] <= '9' && message[3] == ':' && message[4] == ' ';
        }
        return exception is IOException || exception is SocketException || exception is TimeoutException ||
            exception is SslHandshakeException || exception is System.Security.Authentication.AuthenticationException ||
            exception is SmtpProtocolException || exception is OperationCanceledException;
    }

    private static SmtpResult Failure(MimeMessage message, string server, int port, string error, bool transient, bool unknown) =>
        new(false, EmailAction.Send, message.To.ToString(), message.From.ToString(), server, port, TimeSpan.Zero, error: error) {
            MessageId = message.MessageId, IsTransient = transient, DeliveryUnknown = unknown
        };

    private static void AddAttempt(List<SmtpDeliveryAttempt> attempts, HostState host, int number, SmtpResult result) =>
        attempts.Add(new SmtpDeliveryAttempt {
            Server = host.Endpoint.Server, Port = host.Endpoint.Port, Number = number,
            Succeeded = result.Status || result.DeliveryAccepted, IsTransient = result.IsTransient,
            Error = result.Error, DeliveryUnknown = result.DeliveryUnknown
        });

    private static SmtpResult Finish(SmtpResult result, List<SmtpDeliveryAttempt> attempts, TimeSpan elapsed) {
        result.Attempts = attempts.AsReadOnly();
        result.TimeToExecute = elapsed;
        return result;
    }

    private static void DropConnection(HostState host) {
        var smtp = host.Connection;
        host.Connection = null;
        if (smtp == null) return;
        try { smtp.Dispose(); } catch { /* Disposal must not replace the delivery decision. */ }
    }

    /// <summary>Waits for an active send and releases all owned connections. Later sends are rejected.</summary>
    public void Dispose() {
        gate.Wait();
        try {
            if (disposed) return;
            disposed = true;
            foreach (var host in hosts) DropConnection(host);
            transportIdentity = null;
        } finally {
            gate.Release();
        }
    }
}
