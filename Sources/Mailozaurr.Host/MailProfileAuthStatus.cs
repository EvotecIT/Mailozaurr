namespace Mailozaurr.Hosting;

/// <summary>
/// Describes the persisted authentication state for a reusable Mailozaurr profile.
/// </summary>
public sealed class MailProfileAuthStatus {
    /// <summary>The stable profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>The provider kind associated with the profile.</summary>
    public MailProfileKind ProfileKind { get; set; }

    /// <summary>The persisted shared auth-flow marker, when available.</summary>
    public string? AuthFlow { get; set; }

    /// <summary>A normalized auth mode summary such as interactive, appOnly, manualToken, basic, or unknown.</summary>
    public string Mode { get; set; } = "unknown";

    /// <summary>An optional persisted login hint used by interactive OAuth flows.</summary>
    public string? LoginHint { get; set; }

    /// <summary>The effective mailbox or account identifier associated with the profile.</summary>
    public string? Mailbox { get; set; }

    /// <summary>The persisted access-token expiration timestamp, when known.</summary>
    public DateTimeOffset? TokenExpiresOn { get; set; }

    /// <summary>Whether an access token is currently stored.</summary>
    public bool HasAccessToken { get; set; }

    /// <summary>Whether a refresh token is currently stored.</summary>
    public bool HasRefreshToken { get; set; }

    /// <summary>Whether a client secret is currently stored.</summary>
    public bool HasClientSecret { get; set; }

    /// <summary>Whether a certificate path is configured.</summary>
    public bool HasCertificatePath { get; set; }

    /// <summary>Whether a certificate password is currently stored.</summary>
    public bool HasCertificatePassword { get; set; }

    /// <summary>Whether a basic password secret is currently stored.</summary>
    public bool HasPassword { get; set; }

    /// <summary>Whether a client id is configured.</summary>
    public bool HasClientId { get; set; }

    /// <summary>Whether a tenant id is configured.</summary>
    public bool HasTenantId { get; set; }

    /// <summary>Whether the persisted token is already expired.</summary>
    public bool IsTokenExpired { get; set; }

    /// <summary>Whether the shared auth service can perform refresh-auth for this profile.</summary>
    public bool CanRefresh { get; set; }

    /// <summary>Whether the shared auth service has enough information to run an interactive login flow.</summary>
    public bool CanLoginInteractively { get; set; }

    /// <summary>A short human-readable summary of the current auth posture.</summary>
    public string Summary { get; set; } = string.Empty;
}