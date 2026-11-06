using System.Globalization;
using System.Net;
using System.Net.Mail;

namespace Mailozaurr;

/// <summary>Validates and snapshots typed delivery configuration, then sends through named routes.</summary>
public sealed class MailDeliveryEngine {
    private readonly Dictionary<string, DeliveryProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string[]> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly IMailCredentialResolver _credentials;
    private static readonly HashSet<string> RoutingHeaders = new(StringComparer.OrdinalIgnoreCase) {
        "From", "Sender", "To", "Cc", "Bcc", "Return-Path"
    };

    /// <summary>Creates an engine from options constructed in code or loaded from JSON.</summary>
    /// <param name="configuration">Transport profiles and ordered routes.</param>
    /// <param name="credentials">Optional resolver for deployment-specific secret sources.</param>
    public MailDeliveryEngine(MailDeliveryConfiguration configuration, IMailCredentialResolver? credentials = null) {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        _credentials = credentials ?? new MailCredentialResolver();
        foreach (var entry in configuration.Profiles ?? throw new ArgumentException("Profiles must be an object.", nameof(configuration))) {
            ValidateId(entry.Key);
            _profiles.Add(entry.Key, BuildProfile(entry.Key, entry.Value));
        }

        foreach (var entry in configuration.Routes ?? throw new ArgumentException("Routes must be an object.", nameof(configuration))) {
            ValidateId(entry.Key);
            var ids = entry.Value?.ToArray() ?? Array.Empty<string>();
            if (ids.Length == 0 || ids.Any(id => string.IsNullOrEmpty(id) || !_profiles.ContainsKey(id)) ||
                ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Length)
                throw new ArgumentException($"Route '{entry.Key}' needs distinct, configured profile IDs.", nameof(configuration));
            _routes.Add(entry.Key, ids);
        }
    }

    /// <summary>Checks whether a route is available without reading credentials or contacting providers.</summary>
    public bool ContainsRoute(string routeId) => routeId != null && _routes.ContainsKey(routeId);

    /// <summary>Sends once through a named route. Automatic fallback requires positive evidence of non-acceptance.</summary>
    /// <remarks>The route supplies the sender identity. Cancellation stops routing and propagates to the caller.</remarks>
    public async Task<MailDeliveryResult> SendAsync(string routeId, DraftMessage message, CancellationToken cancellationToken = default) {
        if (message == null) throw new ArgumentNullException(nameof(message));
        if (message.From != null || message.Headers?.Keys.Any(IsRoutingHeader) == true)
            throw new ArgumentException("Use the route's sender and the draft's typed recipient properties.", nameof(message));
        if (!_routes.TryGetValue(routeId, out var ids))
            throw new ArgumentException("The mail delivery route is not configured.", nameof(routeId));

        var attempts = new List<MailDeliveryAttempt>(ids.Length);
        foreach (string id in ids) {
            cancellationToken.ThrowIfCancellationRequested();
            MailDeliveryAttempt attempt = await SendAttemptAsync(_profiles[id], message, cancellationToken).ConfigureAwait(false);
            attempts.Add(attempt);
            if (attempt.Outcome != MailDeliveryOutcome.SafeToFallback) break;
        }
        return new MailDeliveryResult(routeId, attempts.AsReadOnly());
    }

    private async Task<MailDeliveryAttempt> SendAttemptAsync(DeliveryProfile configured, DraftMessage message, CancellationToken cancellationToken) {
        MailProfile profile = configured.Profile;
        var secrets = new InMemoryMailSecretStore();
        if (configured.Credential is { } reference) {
            try {
                string? value = await _credentials.ResolveAsync(reference, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(value))
                    return Failure(profile, MailDeliveryOutcome.SafeToFallback, "credential_unavailable");
                await secrets.SetSecretAsync(profile.Id, configured.SecretName!, value!, cancellationToken).ConfigureAwait(false);
            } catch (Exception exception) when (IsOperational(exception, cancellationToken)) {
                return Failure(profile, MailDeliveryOutcome.SafeToFallback, "credential_unavailable");
            }
        }

        try {
            if (profile.Kind == MailProfileKind.Graph)
                return await SendGraphAsync(profile, secrets, message, cancellationToken).ConfigureAwait(false);

            var handler = new SmtpMailSendHandler(new SmtpSessionFactory(secrets));
            var result = await handler.SendAsync(profile, new SendMessageRequest {
                ProfileId = profile.Id,
                Message = message
            }, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded && !result.Queued)
                return new MailDeliveryAttempt(profile.Id, profile.Kind, MailDeliveryOutcome.Accepted, ProviderMessageId: result.ProviderMessageId);

            return result.DeliveryRejected && !result.DeliveryUnknown && !result.Queued
                ? Failure(profile, MailDeliveryOutcome.SafeToFallback, "smtp_rejected")
                : Failure(profile, MailDeliveryOutcome.Unconfirmed, "delivery_unconfirmed");
        } catch (Exception exception) when (IsOperational(exception, cancellationToken)) {
            return Failure(profile, MailDeliveryOutcome.Unconfirmed, "delivery_unconfirmed");
        }
    }

    private static async Task<MailDeliveryAttempt> SendGraphAsync(
        MailProfile profile, IMailSecretStore secrets, DraftMessage message, CancellationToken cancellationToken) {
        GraphSession session;
        try {
            var factory = new GraphSessionFactory(secrets, connectAsync: GraphSessionFactory.ConnectForSingleSendAsync);
            session = await factory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        } catch (Exception exception) when (IsOperational(exception, cancellationToken)) {
            return Failure(profile, MailDeliveryOutcome.SafeToFallback, "graph_connection_failed");
        }

        using (session) {
            using var mime = await new DraftMimeMessageFactory().CreateAsync(profile, message, cancellationToken).ConfigureAwait(false);
            HttpStatusCode response = await session.Client.SendMimeMessageAsync(mime, session.UserId, cancellationToken).ConfigureAwait(false);
            if (response == HttpStatusCode.Accepted)
                return new MailDeliveryAttempt(profile.Id, profile.Kind, MailDeliveryOutcome.Accepted);
            // These responses reject the submission. A server error may follow an accepted request.
            return (int)response is 401 or 403 or 413 or 429
                ? Failure(profile, MailDeliveryOutcome.SafeToFallback, "graph_rejected")
                : Failure(profile, MailDeliveryOutcome.Unconfirmed, "delivery_unconfirmed");
        }
    }

    private static bool IsRoutingHeader(string header) {
        string name = header.Trim();
        return RoutingHeaders.Contains(name) || name.StartsWith("Resent-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOperational(Exception exception, CancellationToken token) =>
        !token.IsCancellationRequested && exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;

    private static MailDeliveryAttempt Failure(MailProfile profile, MailDeliveryOutcome outcome, string code) =>
        new(profile.Id, profile.Kind, outcome, code);

    private static DeliveryProfile BuildProfile(string id, MailDeliveryProfile options) {
        if (options == null) throw new ArgumentException($"Profile '{id}' must be an object.");
        string from = options.From?.Trim() ?? string.Empty;
        MailAddress address;
        try {
            address = new MailAddress(from);
        } catch (Exception exception) when (exception is ArgumentException or FormatException) {
            throw new ArgumentException($"Profile '{id}' requires an individual sender address.");
        }
        if (address.Address != from || !from.Contains("@") || from.Any(char.IsControl))
            throw new ArgumentException($"Profile '{id}' requires an individual sender address.");
        if ((options.Graph == null) == (options.Smtp == null))
            throw new ArgumentException($"Profile '{id}' requires exactly one transport.");

        var profile = new MailProfile { Id = id, DisplayName = id, DefaultSender = from };
        if (options.Graph is { } graph) {
            if (graph.TenantId == Guid.Empty || graph.ClientId == Guid.Empty)
                throw new ArgumentException($"Graph profile '{id}' requires tenant and client IDs.");
            ValidateCredential(id, graph.ClientSecret);
            profile.Kind = MailProfileKind.Graph;
            profile.DefaultMailbox = from;
            profile.Settings[MailProfileSettingsKeys.ClientId] = graph.ClientId.ToString();
            profile.Settings[MailProfileSettingsKeys.TenantId] = graph.TenantId.ToString();
            return new DeliveryProfile(profile, graph.ClientSecret, MailSecretNames.ClientSecret);
        }

        SmtpProfileDeliveryOptions smtp = options.Smtp!;
        if (string.IsNullOrWhiteSpace(smtp.Server) || Uri.CheckHostName(smtp.Server.Trim()) == UriHostNameType.Unknown ||
            smtp.Port is < 1 or > 65535 || !Enum.IsDefined(typeof(SmtpSecurityMode), smtp.Security))
            throw new ArgumentException($"SMTP profile '{id}' requires a valid relay endpoint and security mode.");
        bool authenticate = smtp.UserName != null || smtp.Password != null;
        if (authenticate) {
            if (string.IsNullOrWhiteSpace(smtp.UserName) || smtp.Security is not (SmtpSecurityMode.StartTls or SmtpSecurityMode.SslOnConnect))
                throw new ArgumentException($"Authenticated SMTP profile '{id}' requires a username and mandatory TLS.");
            ValidateCredential(id, smtp.Password);
            profile.Settings[MailProfileSettingsKeys.UserName] = smtp.UserName!.Trim();
        }

        profile.Kind = MailProfileKind.Smtp;
        profile.Settings[MailProfileSettingsKeys.Port] = smtp.Port.ToString(CultureInfo.InvariantCulture);
        profile.Settings[MailProfileSettingsKeys.SecureSocketOptions] = smtp.Security.ToString();
        profile.Settings[MailProfileSettingsKeys.AuthenticationEnabled] = authenticate ? "true" : "false";
        profile.SmtpDelivery = new SmtpDeliveryOptions {
            Servers = new List<SmtpEndpoint> { new() { Server = smtp.Server.Trim(), Port = smtp.Port } },
            MaxAttempts = 1
        };
        return new DeliveryProfile(profile, smtp.Password, authenticate ? MailSecretNames.Password : null);
    }

    private static void ValidateId(string id) {
        if (string.IsNullOrWhiteSpace(id) || id != id.Trim() || id.Any(char.IsControl))
            throw new ArgumentException("Mail delivery profile and route IDs must be nonempty and trimmed.");
    }

    private static void ValidateCredential(string id, MailCredentialReference? credential) {
        if (credential == null || string.IsNullOrWhiteSpace(credential.Source) || string.IsNullOrWhiteSpace(credential.Key))
            throw new ArgumentException($"Profile '{id}' requires a credential source and key.");
    }

    private sealed record DeliveryProfile(MailProfile Profile, MailCredentialReference? Credential, string? SecretName);


}
