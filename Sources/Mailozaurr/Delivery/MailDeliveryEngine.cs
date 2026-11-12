using System.Net;
using System.Text.Json;

namespace Mailozaurr;

/// <summary>Runs application-owned mail policies through the canonical provider handlers.</summary>
/// <remarks>The engine owns SMTP connections. Keep it for a batch or service lifetime and dispose it when done.
/// Concurrent sends are serialized, including profile selection, credential changes and connection reuse.</remarks>
public sealed partial class MailDeliveryEngine : IDisposable {
    private readonly Dictionary<string, MailDeliveryProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RouteState> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SmtpDeliverySession> _smtp = new(StringComparer.OrdinalIgnoreCase);
    private readonly IMailCredentialResolver _credentials;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Random _random = new();
    private bool _disposed;

    /// <summary>Snapshots and validates typed configuration without reading credentials or opening a connection.</summary>
    public MailDeliveryEngine(MailDeliveryConfiguration configuration, IMailCredentialResolver? credentials = null) {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        _credentials = credentials ?? new MailCredentialResolver();
        foreach (var entry in configuration.Profiles ?? throw new ArgumentException("Profiles must be an object.")) {
            ValidateId(entry.Key);
            MailDeliveryProfile profile = SnapshotProfile(entry.Key, entry.Value);
            if (_credentials is MailCredentialResolver)
                foreach (MailCredentialReference reference in profile.Credentials.Values) MailCredentialResolver.Validate(reference);
            _profiles.Add(entry.Key, profile);
        }
        foreach (var entry in configuration.Routes ?? throw new ArgumentException("Routes must be an object.")) {
            ValidateId(entry.Key);
            var route = entry.Value ?? throw new ArgumentException("A route must be an object.");
            if (route.Profiles == null || route.Profiles.Count == 0 ||
                route.Profiles.Any(id => string.IsNullOrEmpty(id) || !_profiles.ContainsKey(id)) ||
                route.Profiles.Distinct(StringComparer.OrdinalIgnoreCase).Count() != route.Profiles.Count)
                throw new ArgumentException($"Route '{entry.Key}' needs distinct, configured profile IDs.");
            if (!Enum.IsDefined(typeof(MailDeliveryStrategy), route.Strategy) || route.RetryCount is < 0 or > 100 ||
                route.RetryDelay < TimeSpan.Zero || route.MaxRetryDelay < route.RetryDelay ||
                route.MaxRetryDelay.TotalMilliseconds > int.MaxValue ||
                double.IsNaN(route.RetryBackoff) || double.IsInfinity(route.RetryBackoff) || route.RetryBackoff < 1)
                throw new ArgumentException($"Route '{entry.Key}' has an invalid retry or selection policy.");
            _routes.Add(entry.Key, new RouteState(route));
        }
    }

    /// <summary>Checks whether the configured route exists.</summary>
    public bool ContainsRoute(string routeId) => routeId != null && _routes.ContainsKey(routeId);

    /// <summary>Sends with bounded retries, load balancing and fallback. Never automatically resends an uncertain submission.</summary>
    /// <remarks>Recipients and optional sender come from the typed draft. Cancellation during submission can leave acceptance uncertain.</remarks>
    public async Task<MailDeliveryResult> SendAsync(string routeId, DraftMessage message, CancellationToken cancellationToken = default) {
        if (message == null) throw new ArgumentNullException(nameof(message));
        if (message.Headers?.Keys.Any(IsRoutingHeader) == true)
            throw new ArgumentException("Use typed sender and recipient properties instead of routing headers.", nameof(message));
        if (!_routes.TryGetValue(routeId, out var route))
            throw new ArgumentException("The mail delivery route is not configured.", nameof(routeId));
        var draft = DraftMessageCloner.Clone(message);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (_disposed) throw new ObjectDisposedException(nameof(MailDeliveryEngine));
            int start = route.Strategy switch {
                MailDeliveryStrategy.RoundRobin => route.Next,
                MailDeliveryStrategy.Random => _random.Next(route.Profiles.Length),
                _ => 0
            };
            route.Next = (start + 1) % route.Profiles.Length;
            var eligible = new HashSet<string>(route.Profiles, StringComparer.OrdinalIgnoreCase);
            var attempts = new List<MailDeliveryAttempt>();
            for (int pass = 0; pass <= route.RetryCount && eligible.Count > 0; pass++) {
                if (pass > 0) {
                    TimeSpan delay = RetryDelayCalculator.Calculate((int)route.Delay.TotalMilliseconds, route.Backoff,
                        pass - 1, (int)route.MaxDelay.TotalMilliseconds, 0);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                for (int index = 0; index < route.Profiles.Length; index++) {
                    string id = route.Profiles[(start + index) % route.Profiles.Length];
                    if (!eligible.Contains(id)) continue;
                    cancellationToken.ThrowIfCancellationRequested();
                    MailDeliveryAttempt attempt = await SendAttemptAsync(_profiles[id], draft, cancellationToken).ConfigureAwait(false);
                    attempts.Add(attempt);
                    if (attempt.Outcome != MailDeliveryOutcome.SafeToFallback)
                        return new MailDeliveryResult(routeId, attempts.AsReadOnly());
                    if (!attempt.Retryable) eligible.Remove(id);
                }
            }
            return new MailDeliveryResult(routeId, attempts.AsReadOnly());
        } finally {
            _gate.Release();
        }
    }

    private async Task<MailDeliveryAttempt> SendAttemptAsync(MailDeliveryProfile configured, DraftMessage draft, CancellationToken cancellationToken) {
        MailProfile profile = configured.Profile;
        var secrets = new InMemoryMailSecretStore();
        foreach (var reference in configured.Credentials) {
            try {
                string? value = await _credentials.ResolveAsync(reference.Value, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(value))
                    return Failure(profile, MailDeliveryOutcome.SafeToFallback, "credential_unavailable", true);
                await secrets.SetSecretAsync(profile.Id, reference.Key, value!, cancellationToken).ConfigureAwait(false);
            } catch (Exception exception) when (IsOperational(exception, cancellationToken)) {
                return Failure(profile, MailDeliveryOutcome.SafeToFallback, "credential_unavailable", true);
            }
        }

        // Validate the actual message before any transport so a malformed draft never drives provider fallback.
        using var validated = await new DraftMimeMessageFactory().CreateAsync(profile, draft, cancellationToken).ConfigureAwait(false);
        try {
            if (profile.Kind == MailProfileKind.Graph)
                return await SendGraphAsync(profile, secrets, validated, cancellationToken).ConfigureAwait(false);
            if (profile.Kind == MailProfileKind.Gmail)
                return await SendGmailAsync(profile, secrets, validated, cancellationToken).ConfigureAwait(false);
            if (profile.Kind == MailProfileKind.Smtp) {
                var smtp = new SmtpMailSendHandler(new SmtpSessionFactory(secrets), SmtpSession(profile.Id));
                return DeliveryResult(profile, await smtp.SendPreparedAsync(profile, validated, cancellationToken).ConfigureAwait(false));
            }
            IMailSendHandler handler = profile.Kind switch {
                MailProfileKind.SendGrid => new SendGridMailSendHandler(secrets, singleAttempt: true),
                MailProfileKind.Mailgun => new MailgunMailSendHandler(secrets, singleAttempt: true),
                MailProfileKind.Ses => new SesMailSendHandler(secrets, singleAttempt: true),
                _ => throw new NotSupportedException("The profile is not a send provider.")
            };
            SendResult result = await handler.SendAsync(profile, new SendMessageRequest { ProfileId = profile.Id, Message = draft },
                cancellationToken).ConfigureAwait(false);
            return DeliveryResult(profile, result);
        } catch (Exception exception) when (IsOperational(exception, cancellationToken)) {
            return Failure(profile, MailDeliveryOutcome.Unconfirmed, "delivery_unconfirmed");
        }
    }

    private static MailDeliveryAttempt DeliveryResult(MailProfile profile, SendResult result) {
        if (result.Succeeded && !result.Queued)
            return new MailDeliveryAttempt(profile.Id, profile.Kind, MailDeliveryOutcome.Accepted, ProviderMessageId: result.ProviderMessageId);
        return result.DeliveryRejected && !result.DeliveryUnknown && !result.Queued
            ? Failure(profile, MailDeliveryOutcome.SafeToFallback, "provider_rejected", result.IsTransient)
            : Failure(profile, MailDeliveryOutcome.Unconfirmed, "delivery_unconfirmed");
    }

    private SmtpDeliverySession SmtpSession(string id) {
        if (!_smtp.TryGetValue(id, out var session)) _smtp.Add(id, session = new SmtpDeliverySession());
        return session;
    }

    private static async Task<MailDeliveryAttempt> SendGraphAsync(
        MailProfile profile, IMailSecretStore secrets, MimeKit.MimeMessage mime, CancellationToken cancellationToken) {
        GraphSession session;
        try {
            var factory = new GraphSessionFactory(secrets, connectAsync: GraphSessionFactory.ConnectForSingleSendAsync);
            session = await factory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        } catch (Exception exception) when (IsOperational(exception, cancellationToken)) {
            return Failure(profile, MailDeliveryOutcome.SafeToFallback, "graph_connection_failed", true);
        }
        using (session) {
            HttpStatusCode status = await session.Client.SendMimeMessageAsync(mime, session.UserId, cancellationToken).ConfigureAwait(false);
            if (status == HttpStatusCode.Accepted)
                return new MailDeliveryAttempt(profile.Id, profile.Kind, MailDeliveryOutcome.Accepted);
            return (int)status is 400 or 401 or 403 or 404 or 413 or 422 or 429
                ? Failure(profile, MailDeliveryOutcome.SafeToFallback, "graph_rejected", (int)status == 429)
                : Failure(profile, MailDeliveryOutcome.Unconfirmed, "delivery_unconfirmed");
        }
    }

    private static async Task<MailDeliveryAttempt> SendGmailAsync(
        MailProfile profile, IMailSecretStore secrets, MimeKit.MimeMessage mime, CancellationToken cancellationToken) {
        GmailSession session;
        try {
            var factory = new GmailSessionFactory(secrets, connectAsync: GmailSessionFactory.ConnectForSingleSendAsync);
            session = await factory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        } catch (Exception exception) when (IsOperational(exception, cancellationToken)) {
            return Failure(profile, MailDeliveryOutcome.SafeToFallback, "gmail_connection_failed", true);
        }
        using (session) {
            try {
                GmailMessage sent = await session.Client.SendAsync(session.UserId, mime, cancellationToken).ConfigureAwait(false);
                return new MailDeliveryAttempt(profile.Id, profile.Kind, MailDeliveryOutcome.Accepted, ProviderMessageId: sent.Id);
            } catch (GmailAuthenticationException) {
                return Failure(profile, MailDeliveryOutcome.SafeToFallback, "gmail_rejected");
            } catch (GmailSubmissionException exception) {
                return (int)exception.SubmissionStatus is >= 400 and < 500 && (int)exception.SubmissionStatus != 408
                    ? Failure(profile, MailDeliveryOutcome.SafeToFallback, "gmail_rejected", (int)exception.SubmissionStatus == 429)
                    : Failure(profile, MailDeliveryOutcome.Unconfirmed, "delivery_unconfirmed");
            }
        }
    }

    private static bool IsRoutingHeader(string header) {
        string name = header.Trim();
        return name.Equals("From", StringComparison.OrdinalIgnoreCase) || name.Equals("Sender", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("To", StringComparison.OrdinalIgnoreCase) || name.Equals("Cc", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Bcc", StringComparison.OrdinalIgnoreCase) || name.Equals("Return-Path", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Resent-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOperational(Exception exception, CancellationToken token) =>
        !token.IsCancellationRequested && exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;

    private static MailDeliveryAttempt Failure(MailProfile profile, MailDeliveryOutcome outcome, string code, bool retryable = false) =>
        new(profile.Id, profile.Kind, outcome, code, Retryable: retryable);

    /// <summary>Closes owned SMTP connections after outstanding delivery completes.</summary>
    public void Dispose() {
        _gate.Wait();
        try {
            if (_disposed) return;
            _disposed = true;
            foreach (var session in _smtp.Values) session.Dispose();
        } finally {
            _gate.Release();
        }
    }

    private sealed class RouteState(MailDeliveryRoute route) {
        internal readonly string[] Profiles = route.Profiles.ToArray();
        internal readonly MailDeliveryStrategy Strategy = route.Strategy;
        internal readonly int RetryCount = route.RetryCount;
        internal readonly TimeSpan Delay = route.RetryDelay;
        internal readonly double Backoff = route.RetryBackoff;
        internal readonly TimeSpan MaxDelay = route.MaxRetryDelay;
        internal int Next;
    }
}
