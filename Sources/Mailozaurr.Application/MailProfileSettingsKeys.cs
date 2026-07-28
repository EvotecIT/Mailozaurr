namespace Mailozaurr.Application;

/// <summary>
/// Common well-known setting keys used by profile-based adapters.
/// </summary>
public static class MailProfileSettingsKeys {
    /// <summary>User name used for authentication.</summary>
    public const string UserName = "userName";

    /// <summary>Server host name.</summary>
    public const string Server = "server";

    /// <summary>Server port number.</summary>
    public const string Port = "port";

    /// <summary>Default folder or folder path.</summary>
    public const string Folder = "folder";

    /// <summary>Tenant identifier or directory id.</summary>
    public const string TenantId = "tenantId";

    /// <summary>Client or application identifier.</summary>
    public const string ClientId = "clientId";

    /// <summary>Certificate path for certificate-based authentication.</summary>
    public const string CertificatePath = "certificatePath";

    /// <summary>Redirect URI used by interactive OAuth flows.</summary>
    public const string RedirectUri = "redirectUri";

    /// <summary>Authentication flow identifier for provider-specific login orchestration.</summary>
    public const string AuthFlow = "authFlow";

    /// <summary>Preferred login hint used by interactive authentication flows.</summary>
    public const string LoginHint = "loginHint";

    /// <summary>Access-token expiration timestamp in round-trip format.</summary>
    public const string TokenExpiresOn = "tokenExpiresOn";

    /// <summary>Mailbox address or user principal name.</summary>
    public const string Mailbox = "mailbox";

    /// <summary>Authentication mode identifier.</summary>
    public const string AuthMode = "authMode";

    /// <summary>Whether the transport should authenticate after connecting.</summary>
    public const string AuthenticationEnabled = "authenticationEnabled";

    /// <summary>Secure socket options mode.</summary>
    public const string SecureSocketOptions = "secureSocketOptions";

    /// <summary>Compatibility flag that requests SSL/TLS when explicit socket options are not supplied.</summary>
    public const string UseSsl = "useSsl";

    /// <summary>Connection timeout in milliseconds.</summary>
    public const string Timeout = "timeout";

    /// <summary>Retry count for transient connection failures.</summary>
    public const string RetryCount = "retryCount";

    /// <summary>Retry delay in milliseconds.</summary>
    public const string RetryDelayMilliseconds = "retryDelayMilliseconds";

    /// <summary>Retry backoff multiplier.</summary>
    public const string RetryDelayBackoff = "retryDelayBackoff";

    /// <summary>Maximum retry delay in milliseconds.</summary>
    public const string MaxDelayMilliseconds = "maxDelayMilliseconds";

    /// <summary>Random retry jitter window in milliseconds.</summary>
    public const string JitterMilliseconds = "jitterMilliseconds";

    /// <summary>Whether non-transient failures should also be retried.</summary>
    public const string RetryAlways = "retryAlways";

    /// <summary>Provider sending domain, where the transport supports an explicit domain.</summary>
    public const string Domain = "domain";

    /// <summary>Cloud region used by a regional transport such as Amazon SES.</summary>
    public const string Region = "region";

    /// <summary>Whether certificate revocation checks should be skipped.</summary>
    public const string SkipCertificateRevocation = "skipCertificateRevocation";

    /// <summary>Whether certificate validation should be skipped.</summary>
    public const string SkipCertificateValidation = "skipCertificateValidation";

    /// <summary>Maximum message body size used when reading messages.</summary>
    public const string MaxBodyBytes = "maxBodyBytes";
}
