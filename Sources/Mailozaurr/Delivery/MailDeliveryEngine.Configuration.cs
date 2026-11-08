using MailKit.Security;

namespace Mailozaurr;

public sealed partial class MailDeliveryEngine {
    private static MailDeliveryProfile SnapshotProfile(string id, MailDeliveryProfile configured) {
        if (configured?.Profile?.Settings == null || configured.Credentials == null)
            throw new ArgumentException($"Profile '{id}' requires a profile and credential map.");
        MailProfile profile = MailProfileCloner.Clone(configured.Profile);
        if (profile.Id.Length > 0 && !profile.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Profile '{id}' must use the same ID as its configuration key.");
        profile.Id = id;
        if (string.IsNullOrWhiteSpace(profile.DisplayName)) profile.DisplayName = id;
        MailProfileValidationResult validation = MailProfileValidator.Validate(profile);
        if (!validation.Succeeded) throw new ArgumentException(string.Join(" ", validation.Errors));
        if (profile.Kind is not (MailProfileKind.Graph or MailProfileKind.Smtp or MailProfileKind.Gmail or
            MailProfileKind.SendGrid or MailProfileKind.Mailgun or MailProfileKind.Ses))
            throw new ArgumentException($"Profile '{id}' must support sending.");
        if (!profile.GetCapabilities().Supports(MailCapability.SendMessages))
            throw new ArgumentException($"Profile '{id}' disables sending.");
        if (profile.Kind == MailProfileKind.Graph && string.IsNullOrWhiteSpace(profile.DefaultMailbox))
            profile.DefaultMailbox = profile.DefaultSender;

        var credentials = new Dictionary<string, MailCredentialReference>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in configured.Credentials) {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null ||
                string.IsNullOrWhiteSpace(entry.Value.Source) || string.IsNullOrWhiteSpace(entry.Value.Key))
                throw new ArgumentException($"Profile '{id}' has an invalid credential reference.");
            credentials.Add(entry.Key, entry.Value);
        }
        ValidateAuthentication(profile, credentials);
        // The route owns resubmission. Provider-level retry is disabled to avoid layered attempt budgets.
        profile.Settings[MailProfileSettingsKeys.RetryCount] = "0";
        profile.Settings[MailProfileSettingsKeys.RetryAlways] = "false";
        if (profile.Kind == MailProfileKind.Smtp) {
            bool authenticate = !profile.Settings.TryGetValue(MailProfileSettingsKeys.AuthenticationEnabled, out var enabled) ||
                !bool.TryParse(enabled, out bool value) || value;
            SecureSocketOptions security = SecureSocketOptions.Auto;
            if (profile.Settings.TryGetValue(MailProfileSettingsKeys.SecureSocketOptions, out var raw) &&
                !Enum.TryParse(raw, true, out security))
                throw new ArgumentException($"SMTP profile '{id}' has invalid transport security.");
            if (profile.Settings.TryGetValue(MailProfileSettingsKeys.UseSsl, out var ssl) && bool.TryParse(ssl, out bool useSsl))
                security = security.ResolveEffective(useSsl);
            if (profile.SmtpDelivery == null) {
                int port = SmtpSessionFactory.GetIntSetting(profile, MailProfileSettingsKeys.Port) ?? 587;
                profile.SmtpDelivery = new SmtpDeliveryOptions {
                    Servers = new() { SmtpEndpoint.Parse(profile.Settings[MailProfileSettingsKeys.Server], port, security) },
                    MaxAttempts = 1,
                    CircuitBreakMinutes = 0
                };
            }
            SmtpSessionFactory.ValidateTransportSettings(profile);
            SmtpEndpoint[] endpoints = profile.SmtpDelivery.ResolveEndpoints(security);
            if (authenticate && endpoints.Any(endpoint => endpoint.SecureSocketOptions is not (SecureSocketOptions.StartTls or SecureSocketOptions.SslOnConnect)))
                throw new ArgumentException($"Authenticated SMTP profile '{id}' requires mandatory TLS on every server.");
        }
        if (profile.Kind is MailProfileKind.SendGrid or MailProfileKind.Mailgun or MailProfileKind.Ses)
            ProviderMailSendHandlerSupport.ApplyRetrySettings(profile, static (_, _, _, _, _, _) => { });
        return new MailDeliveryProfile { Profile = profile, Credentials = credentials };
    }

    private static void ValidateAuthentication(MailProfile profile, Dictionary<string, MailCredentialReference> credentials) {
        bool HasSetting(string key) => profile.Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);
        bool HasSecret(string key) => credentials.ContainsKey(key);
        bool valid = profile.Kind switch {
            MailProfileKind.Graph => HasSecret(MailSecretNames.AccessToken) ||
                HasSetting(MailProfileSettingsKeys.TenantId) && HasSetting(MailProfileSettingsKeys.ClientId) &&
                (HasSecret(MailSecretNames.ClientSecret) || HasSetting(MailProfileSettingsKeys.CertificatePath)),
            MailProfileKind.Gmail => HasSecret(MailSecretNames.AccessToken) ||
                HasSetting(MailProfileSettingsKeys.ClientId) && HasSecret(MailSecretNames.RefreshToken) && HasSecret(MailSecretNames.ClientSecret),
            MailProfileKind.SendGrid or MailProfileKind.Mailgun => HasSecret(MailSecretNames.ApiKey),
            MailProfileKind.Ses => HasSecret(MailSecretNames.AccessKeyId) && HasSecret(MailSecretNames.SecretAccessKey),
            MailProfileKind.Smtp => ValidateSmtpAuthentication(profile, credentials),
            _ => false
        };
        if (!valid) throw new ArgumentException($"Profile '{profile.Id}' is missing required authentication settings or credential references.");
    }

    private static bool ValidateSmtpAuthentication(MailProfile profile, Dictionary<string, MailCredentialReference> credentials) {
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.AuthenticationEnabled, out var raw)) {
            if (!bool.TryParse(raw, out bool enabled))
                throw new ArgumentException($"SMTP profile '{profile.Id}' requires a boolean authenticationEnabled value.");
            if (!enabled) return true;
        }
        _ = SmtpSessionFactory.ResolveUserName(profile);
        bool oauth = SmtpSessionFactory.GetAuthMode(profile) == ProtocolAuthMode.OAuth2;
        return credentials.ContainsKey(MailSecretNames.Password) || oauth && credentials.ContainsKey(MailSecretNames.AccessToken);
    }

    private static void ValidateId(string id) {
        if (string.IsNullOrWhiteSpace(id) || id != id.Trim() || id.Any(char.IsControl))
            throw new ArgumentException("Mail delivery profile and route IDs must be nonempty and trimmed.");
    }
}
