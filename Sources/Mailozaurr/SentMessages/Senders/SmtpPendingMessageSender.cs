using System.Globalization;

namespace Mailozaurr;

/// <summary>
/// Sends queued SMTP messages using <see cref="ClientSmtp"/>.
/// </summary>
public sealed class SmtpPendingMessageSender : IPendingMessageSender {
    private const string SecureSocketOptionsKey = "SecureSocketOptions";
    private const string UseSslKey = "UseSsl";
    private const string SkipCertificateValidationKey = "SkipCertificateValidation";
    private const string CheckCertificateRevocationKey = "CheckCertificateRevocation";
    private const string TimeoutKey = "TimeoutMilliseconds";

    private readonly Func<ClientSmtp> clientFactory;
    private readonly SecureSocketOptions defaultSecureSocketOptions;
    private readonly bool defaultUseSsl;
    private readonly bool defaultSkipCertificateValidation;
    private readonly bool defaultCheckCertificateRevocation;
    private readonly int? defaultTimeout;
    private readonly ICredentialProtector credentialProtector;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpPendingMessageSender"/> class.
    /// </summary>
    /// <param name="clientFactory">Factory used to create <see cref="ClientSmtp"/> instances.</param>
    /// <param name="secureSocketOptions">Default secure socket options.</param>
    /// <param name="useSsl">Whether SSL should be forced when options are auto.</param>
    /// <param name="skipCertificateValidation">Whether certificate validation should be skipped.</param>
    /// <param name="checkCertificateRevocation">Whether certificate revocation should be checked.</param>
    /// <param name="timeout">Optional operation timeout in milliseconds.</param>
    /// <param name="credentialProtector">Optional credential protector used to decrypt queued passwords.</param>
    public SmtpPendingMessageSender(
        Func<ClientSmtp>? clientFactory = null,
        SecureSocketOptions secureSocketOptions = SecureSocketOptions.Auto,
        bool useSsl = false,
        bool skipCertificateValidation = false,
        bool checkCertificateRevocation = true,
        int? timeout = null,
        ICredentialProtector? credentialProtector = null) {
        this.clientFactory = clientFactory ?? (() => Smtp.ClientFactory(null));
        defaultSecureSocketOptions = secureSocketOptions;
        defaultUseSsl = useSsl;
        defaultSkipCertificateValidation = skipCertificateValidation;
        defaultCheckCertificateRevocation = checkCertificateRevocation;
        defaultTimeout = timeout;
        this.credentialProtector = credentialProtector ?? CredentialProtection.Default;
    }

    /// <inheritdoc />
    public async Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }
        if (string.IsNullOrWhiteSpace(record.MimeMessage)) {
            throw new InvalidOperationException("Pending message does not contain a MIME payload.");
        }
        if (string.IsNullOrWhiteSpace(record.Server)) {
            throw new InvalidOperationException("Pending message is missing SMTP server information.");
        }

        var message = await LoadMessageAsync(record, ct).ConfigureAwait(false);
        var secureSocketOptions = ResolveSecureSocketOptions(record.ProviderData);
        var useSsl = ResolveBool(record.ProviderData, UseSslKey, defaultUseSsl);
        if (useSsl && secureSocketOptions == SecureSocketOptions.Auto) {
            secureSocketOptions = SecureSocketOptions.StartTls;
        }
        var skipValidation = ResolveBool(record.ProviderData, SkipCertificateValidationKey, defaultSkipCertificateValidation);
        var checkRevocation = ResolveBool(record.ProviderData, CheckCertificateRevocationKey, defaultCheckCertificateRevocation);
        var timeout = ResolveInt(record.ProviderData, TimeoutKey, defaultTimeout);

        var port = record.Port ?? 25;
        using var client = clientFactory();
        if (timeout.HasValue) {
            client.Timeout = timeout.Value;
        }
        if (skipValidation) {
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;
        }
        client.CheckCertificateRevocation = checkRevocation;

        try {
            await client.ConnectAsync(record.Server!, port, secureSocketOptions, ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(record.UserName)) {
                var password = DecodePassword(record.Password);
                await client.AuthenticateAsync(record.UserName!, password, ct).ConfigureAwait(false);
            }
            await client.SendAsync(message, ct).ConfigureAwait(false);
        } finally {
            try {
                if (client.IsConnected) {
                    await client.DisconnectAsync(true, ct).ConfigureAwait(false);
                }
            } catch {
                // Ignored - disconnect best effort.
            }
        }
    }

    private static async Task<MimeMessage> LoadMessageAsync(PendingMessageRecord record, CancellationToken ct) {
        var bytes = Convert.FromBase64String(record.MimeMessage);
        using var stream = new MemoryStream(bytes);
        return await MimeMessage.LoadAsync(stream, ct).ConfigureAwait(false);
    }

    private SecureSocketOptions ResolveSecureSocketOptions(Dictionary<string, string> providerData) {
        if (providerData == null) {
            return defaultSecureSocketOptions;
        }
        if (providerData.TryGetValue(SecureSocketOptionsKey, out var value) && !string.IsNullOrWhiteSpace(value)) {
            if (Enum.TryParse(value, ignoreCase: true, out SecureSocketOptions parsed)) {
                return parsed;
            }
            if (int.TryParse(value, out var numeric) && Enum.IsDefined(typeof(SecureSocketOptions), numeric)) {
                return (SecureSocketOptions)numeric;
            }
        }
        return defaultSecureSocketOptions;
    }

    private static bool ResolveBool(Dictionary<string, string> providerData, string key, bool defaultValue) {
        if (providerData != null && providerData.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) {
            if (bool.TryParse(value, out var parsed)) {
                return parsed;
            }
            if (int.TryParse(value, out var numeric)) {
                return numeric != 0;
            }
        }
        return defaultValue;
    }

    private static int? ResolveInt(Dictionary<string, string> providerData, string key, int? defaultValue) {
        if (providerData != null && providerData.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) {
                return parsed;
            }
        }
        return defaultValue;
    }

    internal string DecodePassword(string? password) => CredentialProtection.UnprotectWithFallback(credentialProtector, password);
}
