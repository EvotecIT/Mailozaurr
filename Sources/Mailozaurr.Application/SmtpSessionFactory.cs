using MailKit.Security;
using Mailozaurr;

namespace Mailozaurr.Application;

/// <summary>
/// Creates authenticated SMTP sessions using profile settings and stored secrets.
/// </summary>
public sealed class SmtpSessionFactory : ISmtpSessionFactory {
    private readonly IMailSecretStore? _secretStore;
    private readonly Func<SmtpSessionRequest, CancellationToken, Task<Smtp>> _connectAsync;

    /// <summary>
    /// Creates a new SMTP session factory.
    /// </summary>
    public SmtpSessionFactory(
        IMailSecretStore? secretStore = null,
        Func<SmtpSessionRequest, CancellationToken, Task<Smtp>>? connectAsync = null) {
        _secretStore = secretStore;
        _connectAsync = connectAsync ?? DefaultConnectAsync;
    }

    /// <inheritdoc />
    public async Task<Smtp> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) {
        if (profile == null) {
            throw new ArgumentNullException(nameof(profile));
        }
        if (profile.Kind != MailProfileKind.Smtp) {
            throw new InvalidOperationException($"Profile '{profile.Id}' is not an SMTP profile.");
        }

        var request = await CreateRequestAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _connectAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SmtpSessionRequest> CreateRequestAsync(MailProfile profile, CancellationToken cancellationToken) {
        var server = RequireSetting(profile, MailProfileSettingsKeys.Server);
        var port = GetIntSetting(profile, MailProfileSettingsKeys.Port) ?? 587;
        var authenticationEnabled = GetBoolSetting(profile, MailProfileSettingsKeys.AuthenticationEnabled) ?? true;
        var authMode = GetAuthMode(profile);
        var userName = authenticationEnabled ? ResolveUserName(profile) : string.Empty;
        var secret = authenticationEnabled
            ? await ResolveSecretAsync(profile, authMode, cancellationToken).ConfigureAwait(false)
            : string.Empty;

        return new SmtpSessionRequest {
            Server = server,
            Port = port,
            SecureSocketOptions = GetSecureSocketOptions(profile),
            UseSsl = GetBoolSetting(profile, MailProfileSettingsKeys.UseSsl) ?? false,
            TimeoutMs = GetIntSetting(profile, MailProfileSettingsKeys.Timeout) ?? 30000,
            RetryCount = GetIntSetting(profile, MailProfileSettingsKeys.RetryCount) ?? 3,
            RetryDelayMilliseconds = GetIntSetting(profile, MailProfileSettingsKeys.RetryDelayMilliseconds) ?? 500,
            RetryDelayBackoff = GetDoubleSetting(profile, MailProfileSettingsKeys.RetryDelayBackoff) ?? 2.0,
            MaxDelayMilliseconds = GetIntSetting(profile, MailProfileSettingsKeys.MaxDelayMilliseconds) ?? 10_000,
            JitterMilliseconds = GetIntSetting(profile, MailProfileSettingsKeys.JitterMilliseconds) ?? 250,
            SkipCertificateValidation = GetBoolSetting(profile, MailProfileSettingsKeys.SkipCertificateValidation) ?? false,
            SkipCertificateRevocation = GetBoolSetting(profile, MailProfileSettingsKeys.SkipCertificateRevocation) ?? false,
            Authenticate = authenticationEnabled,
            UserName = userName,
            Password = secret,
            AuthMode = authMode
        };
    }

    private async Task<string> ResolveSecretAsync(MailProfile profile, ProtocolAuthMode authMode, CancellationToken cancellationToken) {
        var primarySecretName = authMode == ProtocolAuthMode.OAuth2
            ? MailSecretNames.AccessToken
            : MailSecretNames.Password;
        var fallbackSecretName = authMode == ProtocolAuthMode.OAuth2
            ? MailSecretNames.Password
            : null;

        var secret = _secretStore == null
            ? null
            : await _secretStore.GetSecretAsync(profile.Id, primarySecretName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(secret) && fallbackSecretName != null && _secretStore != null) {
            secret = await _secretStore.GetSecretAsync(profile.Id, fallbackSecretName, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(secret)) {
            throw new InvalidOperationException($"Secret '{primarySecretName}' is required for profile '{profile.Id}'.");
        }

        return secret!;
    }

    private static async Task<Smtp> DefaultConnectAsync(SmtpSessionRequest request, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        var smtp = new Smtp();
        var result = await SmtpSessionService.ConnectAndAuthenticateAsync(smtp, request, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) {
            return smtp;
        }

        SmtpSessionService.DisposeQuietly(smtp);
        throw new InvalidOperationException($"SMTP connection/authentication failed ({result.ErrorCode}): {result.Error}");
    }

    private static string ResolveUserName(MailProfile profile) {
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.UserName, out var userName) && !string.IsNullOrWhiteSpace(userName)) {
            return userName.Trim();
        }
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var mailbox) && !string.IsNullOrWhiteSpace(mailbox)) {
            return mailbox.Trim();
        }
        if (!string.IsNullOrWhiteSpace(profile.DefaultMailbox)) {
            return profile.DefaultMailbox!.Trim();
        }
        if (!string.IsNullOrWhiteSpace(profile.DefaultSender)) {
            return profile.DefaultSender!.Trim();
        }

        throw new InvalidOperationException($"Profile '{profile.Id}' requires '{MailProfileSettingsKeys.UserName}' or a mailbox value.");
    }

    private static string RequireSetting(MailProfile profile, string key) {
        if (profile.Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) {
            return value.Trim();
        }

        throw new InvalidOperationException($"Profile '{profile.Id}' requires setting '{key}'.");
    }

    private static ProtocolAuthMode GetAuthMode(MailProfile profile) {
        profile.Settings.TryGetValue(MailProfileSettingsKeys.AuthMode, out var rawMode);
        return ProtocolAuth.ParseMode(rawMode, ProtocolAuthMode.Basic);
    }

    private static SecureSocketOptions GetSecureSocketOptions(MailProfile profile) {
        if (!profile.Settings.TryGetValue(MailProfileSettingsKeys.SecureSocketOptions, out var raw) || string.IsNullOrWhiteSpace(raw)) {
            return SecureSocketOptions.Auto;
        }

        if (Enum.TryParse<SecureSocketOptions>(raw, ignoreCase: true, out var value)) {
            return value;
        }

        throw new InvalidOperationException($"Profile '{profile.Id}' has invalid secure socket option '{raw}'.");
    }

    private static int? GetIntSetting(MailProfile profile, string key) {
        if (!profile.Settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) {
            return null;
        }
        if (int.TryParse(raw, out var value)) {
            return value;
        }

        throw new InvalidOperationException($"Profile '{profile.Id}' has invalid integer setting '{key}'.");
    }

    private static double? GetDoubleSetting(MailProfile profile, string key) {
        if (!profile.Settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) {
            return null;
        }
        if (double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)) {
            return value;
        }

        throw new InvalidOperationException($"Profile '{profile.Id}' has invalid numeric setting '{key}'.");
    }

    private static bool? GetBoolSetting(MailProfile profile, string key) {
        if (!profile.Settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) {
            return null;
        }
        if (bool.TryParse(raw, out var value)) {
            return value;
        }

        throw new InvalidOperationException($"Profile '{profile.Id}' has invalid boolean setting '{key}'.");
    }
}
