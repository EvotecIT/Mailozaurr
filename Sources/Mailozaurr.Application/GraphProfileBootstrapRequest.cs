namespace Mailozaurr.Application;

/// <summary>
/// Request used to create or update a reusable Microsoft Graph profile.
/// </summary>
public sealed class GraphProfileBootstrapRequest {
    /// <summary>Stable profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Human-readable display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional operator-focused description.</summary>
    public string? Description { get; set; }

    /// <summary>Mailbox address or user principal name.</summary>
    public string Mailbox { get; set; } = string.Empty;

    /// <summary>Optional default sender address. Falls back to <see cref="Mailbox" /> when omitted.</summary>
    public string? DefaultSender { get; set; }

    /// <summary>When true, marks the saved profile as the default profile.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Optional Graph client/application identifier.</summary>
    public string? ClientId { get; set; }

    /// <summary>Optional Graph tenant/directory identifier.</summary>
    public string? TenantId { get; set; }

    /// <summary>Optional confidential-client secret to store securely.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Optional explicit access token to store securely.</summary>
    public string? AccessToken { get; set; }

    /// <summary>Optional certificate path for certificate-based authentication.</summary>
    public string? CertificatePath { get; set; }

    /// <summary>Optional certificate password to store securely.</summary>
    public string? CertificatePassword { get; set; }
}
