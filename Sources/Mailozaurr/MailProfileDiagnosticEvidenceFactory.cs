using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Mailozaurr;

/// <summary>Builds normalized diagnostic evidence from provider-native sessions and results.</summary>
internal static class MailProfileDiagnosticEvidenceFactory {
    internal static MailProfileDiagnosticEvidence CreateImapEvidence(ImapClient client) => new() {
        Protocol = "IMAP",
        Session = CreateSessionEvidence(
            client.IsConnected,
            client.IsAuthenticated,
            client.IsSecure,
            client.IsSecure ? client.SslProtocol.ToString() : null,
            client.Capabilities.ToString(),
            client.AuthenticationMechanisms)
    };

    internal static MailProfileDiagnosticEvidence CreatePop3Evidence(Pop3Client client) => new() {
        Protocol = "POP3",
        Session = CreateSessionEvidence(
            client.IsConnected,
            client.IsAuthenticated,
            client.IsSecure,
            client.IsSecure ? client.SslProtocol.ToString() : null,
            client.Capabilities.ToString(),
            client.AuthenticationMechanisms)
    };

    internal static MailProfileDiagnosticEvidence CreateSmtpEvidence(Smtp smtp) => new() {
        Protocol = "SMTP",
        Session = CreateSessionEvidence(
            smtp.Client.IsConnected,
            smtp.Client.IsAuthenticated,
            smtp.Client.IsSecure,
            smtp.Client.IsSecure ? smtp.Client.SslProtocol.ToString() : null,
            smtp.Client.GetCapabilitiesSnapshot().ToString(),
            smtp.Client.AuthenticationMechanisms)
    };

    internal static MailProfileDiagnosticEvidence CreateGraphSessionEvidence(GraphSession session) {
        _ = session;
        return new MailProfileDiagnosticEvidence { Protocol = "Graph" };
    }

    internal static MailProfileDiagnosticEvidence CreateGraphEvidence(GraphSession session, GraphMailboxIdentity identity) {
        var evidence = CreateGraphSessionEvidence(session);
        evidence.Permissions = CreateGraphPermissionEvidence(session.Credential?.AccessToken);
        evidence.Identity = new MailProfileIdentityEvidence {
            Id = identity.Id,
            EmailAddress = string.IsNullOrWhiteSpace(identity.Mail) ? identity.UserPrincipalName : identity.Mail,
            DisplayName = identity.DisplayName,
            Source = "Microsoft Graph users endpoint"
        };
        return evidence;
    }

    internal static MailProfileDiagnosticEvidence CreateGmailSessionEvidence(GmailSession session) {
        _ = session;
        return new MailProfileDiagnosticEvidence { Protocol = "Gmail" };
    }

    internal static MailProfileDiagnosticEvidence CreateGmailEvidence(GmailMailboxBrowser.GmailMailboxProfileResult profile) => new() {
        Protocol = "Gmail",
        Identity = new MailProfileIdentityEvidence {
            EmailAddress = profile.EmailAddress,
            Source = "Gmail users.getProfile endpoint"
        },
        Permissions = new MailProfilePermissionEvidence {
            Source = "unavailable",
            Authoritative = false,
            Detail = "The Gmail profile endpoint verified the credential and identity, but OAuth scope metadata was not available and was not inferred."
        },
        Mailbox = new MailProfileMailboxEvidence {
            MessageCount = profile.MessagesTotal,
            ThreadCount = profile.ThreadsTotal,
            ChangeCursor = profile.HistoryId
        }
    };

    internal static MailProfilePreflightEvidence CreateSendPreflightEvidence(
        MailProfileKind kind,
        MailProfileDiagnosticEvidence evidence) {
        if (kind == MailProfileKind.Smtp) {
            return new MailProfilePreflightEvidence {
                Operation = "send",
                ValidationLevel = "session",
                Ready = evidence.Session?.Connected == true,
                Detail = "The SMTP session and advertised capabilities were inspected. Authentication state is reported separately; no envelope, recipient, or message was submitted."
            };
        }

        var permissionNames = evidence.Permissions?.Names ?? new List<string>();
        var requiredPermission = kind == MailProfileKind.Graph ? "Mail.Send" : null;
        bool? ready = null;
        if (requiredPermission != null && permissionNames.Count > 0) {
            ready = permissionNames.Contains(requiredPermission, StringComparer.OrdinalIgnoreCase);
        }
        return new MailProfilePreflightEvidence {
            Operation = "send",
            ValidationLevel = "provider-identity",
            Ready = ready,
            Detail = requiredPermission == null
                ? "The provider identity endpoint succeeded. Effective send permission was not available, and no message was submitted."
                : "The provider identity endpoint succeeded. Readiness reflects token-declared Mail.Send when claims were available; no message was submitted."
        };
    }

    private static MailProfileSessionEvidence CreateSessionEvidence(
        bool connected,
        bool authenticated,
        bool secure,
        string? tlsProtocol,
        string capabilities,
        IEnumerable<string> authenticationMechanisms) => new() {
        Connected = connected,
        Authenticated = authenticated,
        Secure = secure,
        TlsProtocol = tlsProtocol,
        Capabilities = SplitCapabilities(capabilities),
        AuthenticationMechanisms = authenticationMechanisms
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList()
    };

    private static List<string> SplitCapabilities(string raw) => raw
        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(static value => value.Trim())
        .Where(static value => value.Length > 0 && !value.Equals("None", StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static MailProfilePermissionEvidence CreateGraphPermissionEvidence(string? accessToken) {
        var names = TryReadJwtPermissionNames(accessToken);
        return new MailProfilePermissionEvidence {
            Names = names,
            Source = names.Count == 0 ? "unavailable" : "access-token claims",
            Authoritative = false,
            Detail = names.Count == 0
                ? "The access token was opaque or did not expose scp/roles claims; no permissions were inferred."
                : "These are token-declared scp/roles values. The successful Graph endpoint probe verifies the token, but does not authoritatively enumerate every effective permission."
        };
    }

    private static List<string> TryReadJwtPermissionNames(string? accessToken) {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(accessToken)) {
            return names.ToList();
        }

        var segments = accessToken!.Split('.');
        if (segments.Length < 2) {
            return names.ToList();
        }

        try {
            var payload = segments[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            if (document.RootElement.TryGetProperty("scp", out var scopes) && scopes.ValueKind == JsonValueKind.String) {
                foreach (var scope in (scopes.GetString() ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
                    names.Add(scope);
                }
            }
            if (document.RootElement.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array) {
                foreach (var role in roles.EnumerateArray()) {
                    if (role.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(role.GetString())) {
                        names.Add(role.GetString()!);
                    }
                }
            }
        } catch (FormatException) {
            return new List<string>();
        } catch (JsonException) {
            return new List<string>();
        }

        return names.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
