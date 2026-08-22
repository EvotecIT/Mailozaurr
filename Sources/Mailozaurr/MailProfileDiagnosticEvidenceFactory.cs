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

    internal static MailProfileDiagnosticEvidence CreateGraphEvidence(
        GraphSession session,
        GraphMailboxIdentity? identity = null,
        string? identityUnavailableReason = null) {
        var evidence = CreateGraphSessionEvidence(session);
        evidence.Permissions = CreateGraphPermissionEvidence(session.Credential?.AccessToken);
        evidence.IdentityUnavailableReason = identityUnavailableReason;
        if (identity != null) {
            evidence.Identity = new MailProfileIdentityEvidence {
                Id = identity.Id,
                EmailAddress = string.IsNullOrWhiteSpace(identity.Mail) ? identity.UserPrincipalName : identity.Mail,
                DisplayName = identity.DisplayName,
                Source = "Microsoft Graph users endpoint"
            };
        }
        return evidence;
    }

    internal static MailProfileDiagnosticEvidence CreateGmailSessionEvidence(GmailSession session) {
        _ = session;
        return new MailProfileDiagnosticEvidence { Protocol = "Gmail" };
    }

    internal static MailProfileDiagnosticEvidence CreateGmailCapabilityEvidence(string detail) => new() {
        Protocol = "Gmail",
        Permissions = new MailProfilePermissionEvidence {
            Source = "unavailable",
            Authoritative = false,
            Detail = detail
        }
    };

    internal static MailProfileDiagnosticEvidence CreateGmailEvidence(GmailMailboxBrowser.GmailMailboxProfileResult profile) {
        var evidence = CreateGmailCapabilityEvidence(
            "The Gmail profile endpoint verified the credential and identity, but OAuth scope metadata was not available and was not inferred.");
        ApplyGmailProfile(evidence, profile);
        return evidence;
    }

    internal static void ApplyGmailProfile(
        MailProfileDiagnosticEvidence evidence,
        GmailMailboxBrowser.GmailMailboxProfileResult profile) {
        evidence.Identity = new MailProfileIdentityEvidence {
            EmailAddress = profile.EmailAddress,
            Source = "Gmail users.getProfile endpoint"
        };
        evidence.IdentityUnavailableReason = null;
        evidence.Mailbox ??= new MailProfileMailboxEvidence();
        evidence.Mailbox.MessageCount = profile.MessagesTotal;
        evidence.Mailbox.ThreadCount = profile.ThreadsTotal;
        evidence.Mailbox.ChangeCursor = profile.HistoryId;
    }

    internal static MailProfilePreflightEvidence CreateSendPreflightEvidence(
        MailProfileKind kind,
        MailProfileDiagnosticEvidence evidence,
        string? target = null,
        bool graphMailEndpointSucceeded = true) {
        if (kind == MailProfileKind.Smtp) {
            return new MailProfilePreflightEvidence {
                Operation = "send",
                ValidationLevel = "session",
                Ready = evidence.Session?.Connected == true,
                Detail = "The SMTP session and advertised capabilities were inspected. Authentication state is reported separately; no envelope, recipient, or message was submitted."
            };
        }

        var permissions = evidence.Permissions;
        var delegatedScopes = permissions?.DelegatedScopes ?? new List<string>();
        var applicationRoles = permissions?.ApplicationRoles ?? new List<string>();
        var graphPermissionsKnown = kind == MailProfileKind.Graph &&
            (delegatedScopes.Count > 0 || applicationRoles.Count > 0);
        var applicationReady = HasPermissionPair(applicationRoles, "Mail.ReadWrite", "Mail.Send");
        var delegatedSharedReady = HasPermissionPair(
            delegatedScopes,
            "Mail.ReadWrite.Shared",
            "Mail.Send.Shared");
        var delegatedDirectPair = HasPermissionPair(delegatedScopes, "Mail.ReadWrite", "Mail.Send");
        var targetIsSignedInMailbox = IsSignedInMailbox(target, permissions?.DelegatedIdentity);
        bool? ready = null;
        if (kind == MailProfileKind.Graph) {
            if (!graphMailEndpointSucceeded) {
                ready = false;
            } else if (applicationReady || delegatedSharedReady || (delegatedDirectPair && targetIsSignedInMailbox == true)) {
                ready = true;
            } else if (graphPermissionsKnown && (!delegatedDirectPair || targetIsSignedInMailbox.HasValue)) {
                ready = false;
            }
        }
        return new MailProfilePreflightEvidence {
            Operation = "send",
            ValidationLevel = kind == MailProfileKind.Graph
                ? graphMailEndpointSucceeded ? "mail-endpoint-and-token-claims" : "denied-mail-endpoint-and-token-claims"
                : "provider-response",
            Ready = ready,
            Detail = kind == MailProfileKind.Graph
                ? CreateGraphPreflightDetail(graphMailEndpointSucceeded, targetIsSignedInMailbox)
                : "The provider response was inspected. Effective send permission was not available, and no message was submitted."
        };
    }

    private static bool HasPermissionPair(IReadOnlyCollection<string> permissions, string first, string second) =>
        permissions.Contains(first, StringComparer.OrdinalIgnoreCase) &&
        permissions.Contains(second, StringComparer.OrdinalIgnoreCase);

    private static bool? IsSignedInMailbox(string? target, string? delegatedIdentity) {
        if (string.IsNullOrWhiteSpace(target) || target!.Equals("me", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }
        if (string.IsNullOrWhiteSpace(delegatedIdentity)) {
            return null;
        }
        return target.Trim().Equals(delegatedIdentity!.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateGraphPreflightDetail(bool mailEndpointSucceeded, bool? targetIsSignedInMailbox) {
        var endpoint = mailEndpointSucceeded
            ? "A Graph mail endpoint succeeded."
            : "The bounded Graph mail endpoint probe was denied.";
        var target = targetIsSignedInMailbox switch {
            true => "The selected mailbox matches the delegated sign-in identity (or uses 'me').",
            false => "The selected mailbox differs from the delegated sign-in identity, so delegated direct-mail scopes do not prove shared-mailbox send readiness.",
            null => "The token did not declare enough identity evidence to prove that the selected mailbox is the delegated sign-in mailbox."
        };
        return $"{endpoint} {target} Sending uses draft creation followed by draft send. Application tokens require Mail.ReadWrite plus Mail.Send; delegated access requires that pair for the signed-in mailbox or Mail.ReadWrite.Shared plus Mail.Send.Shared for another mailbox. No draft or message was created.";
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
        var claims = TryReadJwtPermissionClaims(accessToken);
        var names = claims.DelegatedScopes
            .Concat(claims.ApplicationRoles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new MailProfilePermissionEvidence {
            Names = names,
            DelegatedScopes = claims.DelegatedScopes,
            ApplicationRoles = claims.ApplicationRoles,
            DelegatedIdentity = claims.DelegatedIdentity,
            Source = names.Count == 0 ? "unavailable" : "access-token claims",
            Authoritative = false,
            Detail = names.Count == 0
                ? "The access token was opaque or did not expose scp/roles claims; no permissions were inferred."
                : "These are token-declared scp/roles values. The successful Graph endpoint probe verifies the token, but does not authoritatively enumerate every effective permission."
        };
    }

    private static GraphPermissionClaims TryReadJwtPermissionClaims(string? accessToken) {
        var delegatedScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var applicationRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(accessToken)) {
            return new GraphPermissionClaims();
        }

        var segments = accessToken!.Split('.');
        if (segments.Length < 2) {
            return new GraphPermissionClaims();
        }

        try {
            var payload = segments[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            if (document.RootElement.TryGetProperty("scp", out var scopes) && scopes.ValueKind == JsonValueKind.String) {
                foreach (var scope in (scopes.GetString() ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
                    delegatedScopes.Add(scope);
                }
            }
            if (document.RootElement.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array) {
                foreach (var role in roles.EnumerateArray()) {
                    if (role.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(role.GetString())) {
                        applicationRoles.Add(role.GetString()!);
                    }
                }
            }
            var delegatedIdentity = ReadFirstStringClaim(document.RootElement, "preferred_username", "upn", "email");
            return new GraphPermissionClaims {
                DelegatedScopes = delegatedScopes.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToList(),
                ApplicationRoles = applicationRoles.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToList(),
                DelegatedIdentity = delegatedIdentity
            };
        } catch (FormatException) {
            return new GraphPermissionClaims();
        } catch (JsonException) {
            return new GraphPermissionClaims();
        }
    }

    private static string? ReadFirstStringClaim(JsonElement payload, params string[] names) {
        foreach (var name in names) {
            if (payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString())) {
                return value.GetString()!.Trim();
            }
        }
        return null;
    }

    private sealed class GraphPermissionClaims {
        internal List<string> DelegatedScopes { get; set; } = new();
        internal List<string> ApplicationRoles { get; set; } = new();
        internal string? DelegatedIdentity { get; set; }
    }
}
