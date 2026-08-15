namespace Mailozaurr;

/// <summary>
/// Request used to create or update a reusable Gmail profile.
/// </summary>
public sealed class GmailProfileBootstrapRequest {
    /// <summary>Stable profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Human-readable display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional operator-focused description.</summary>
    public string? Description { get; set; }

    /// <summary>Mailbox address or Gmail user id. Defaults to <c>me</c> when omitted.</summary>
    public string? Mailbox { get; set; }

    /// <summary>Optional default sender address. Falls back to the mailbox when omitted.</summary>
    public string? DefaultSender { get; set; }

    /// <summary>When true, marks the saved profile as the default profile.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Optional Google OAuth client identifier.</summary>
    public string? ClientId { get; set; }

    /// <summary>Optional Google OAuth client secret to store securely.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Optional secret reference for the client secret, in the form <c>&lt;profile-id&gt;:&lt;secret-name&gt;</c> or <c>&lt;secret-name&gt;</c>.</summary>
    public string? ClientSecretReference { get; set; }

    /// <summary>Optional refresh token to store securely.</summary>
    public string? RefreshToken { get; set; }

    /// <summary>Optional secret reference for the refresh token, in the form <c>&lt;profile-id&gt;:&lt;secret-name&gt;</c> or <c>&lt;secret-name&gt;</c>.</summary>
    public string? RefreshTokenReference { get; set; }

    /// <summary>Optional explicit access token to store securely.</summary>
    public string? AccessToken { get; set; }

    /// <summary>Optional secret reference for the access token, in the form <c>&lt;profile-id&gt;:&lt;secret-name&gt;</c> or <c>&lt;secret-name&gt;</c>.</summary>
    public string? AccessTokenReference { get; set; }
}