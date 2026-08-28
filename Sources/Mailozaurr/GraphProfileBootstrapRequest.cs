namespace Mailozaurr;

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

    /// <summary>Optional secret reference for the client secret, in the form <c>&lt;profile-id&gt;:&lt;secret-name&gt;</c> or <c>&lt;secret-name&gt;</c>.</summary>
    public string? ClientSecretReference { get; set; }

    /// <summary>Optional explicit access token to store securely.</summary>
    public string? AccessToken { get; set; }

    /// <summary>Optional secret reference for the access token, in the form <c>&lt;profile-id&gt;:&lt;secret-name&gt;</c> or <c>&lt;secret-name&gt;</c>.</summary>
    public string? AccessTokenReference { get; set; }

    /// <summary>Optional certificate path for certificate-based authentication.</summary>
    public string? CertificatePath { get; set; }

    /// <summary>Optional certificate password to store securely.</summary>
    public string? CertificatePassword { get; set; }

    /// <summary>Optional secret reference for the certificate password, in the form <c>&lt;profile-id&gt;:&lt;secret-name&gt;</c> or <c>&lt;secret-name&gt;</c>.</summary>
    public string? CertificatePasswordReference { get; set; }

    /// <summary>Explicitly allows compatible same-name secret references to read from another profile.</summary>
    public bool AllowCrossProfileSecretReferences { get; set; }
}
