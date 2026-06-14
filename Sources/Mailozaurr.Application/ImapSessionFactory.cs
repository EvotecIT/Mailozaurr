using MailKit.Net.Imap;
using MailKit.Security;

namespace Mailozaurr.Application;

/// <summary>
/// Creates authenticated IMAP sessions using profile settings and stored secrets.
/// </summary>
public sealed class ImapSessionFactory : IImapSessionFactory {
    private readonly IMailSecretStore? _secretStore;
    private readonly Func<ImapSessionRequest, CancellationToken, Task<ImapClient>> _connectAsync;

    /// <summary>
    /// Creates a new IMAP session factory.
    /// </summary>
    public ImapSessionFactory(
        IMailSecretStore? secretStore = null,
        Func<ImapSessionRequest, CancellationToken, Task<ImapClient>>? connectAsync = null) {
        _secretStore = secretStore;
        _connectAsync = connectAsync ?? ((request, cancellationToken) => ImapSessionService.ConnectAsync(request, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<ImapClient> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) {
        if (profile == null) {
            throw new ArgumentNullException(nameof(profile));
        }
        if (profile.Kind != MailProfileKind.Imap) {
            throw new InvalidOperationException($"Profile '{profile.Id}' is not an IMAP profile.");
        }

        var request = await CreateRequestAsync(profile, cancellationToken).ConfigureAwait(false);
        return await _connectAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ImapSessionRequest> CreateRequestAsync(MailProfile profile, CancellationToken cancellationToken) {
        var server = RequireSetting(profile, MailProfileSettingsKeys.Server);
        var port = GetIntSetting(profile, MailProfileSettingsKeys.Port) ?? 993;
        var authMode = GetAuthMode(profile);
        var userName = ResolveUserName(profile);
        var secret = await ResolveSecretAsync(profile, authMode, cancellationToken).ConfigureAwait(false);

        return new ImapSessionRequest {
            Connection = new ImapConnectionRequest(
                server,
                port,
                GetSecureSocketOptions(profile),
                GetIntSetting(profile, MailProfileSettingsKeys.Timeout) ?? 30000,
                GetBoolSetting(profile, MailProfileSettingsKeys.SkipCertificateRevocation) ?? false,
                GetBoolSetting(profile, MailProfileSettingsKeys.SkipCertificateValidation) ?? false,
                GetIntSetting(profile, MailProfileSettingsKeys.RetryCount) ?? 3,
                GetIntSetting(profile, MailProfileSettingsKeys.RetryDelayMilliseconds) ?? 500,
                GetDoubleSetting(profile, MailProfileSettingsKeys.RetryDelayBackoff) ?? 2.0),
            UserName = userName,
            Secret = secret,
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

    private static string ResolveUserName(MailProfile profile) {
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.UserName, out var userName) && !string.IsNullOrWhiteSpace(userName)) {
            return userName.Trim();
        }
        if (profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var mailbox) && !string.IsNullOrWhiteSpace(mailbox)) {
            return mailbox.Trim();
        }
        if (!string.IsNullOrWhiteSpace(profile.DefaultMailbox)) {
            var defaultMailbox = profile.DefaultMailbox;
            return defaultMailbox!.Trim();
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