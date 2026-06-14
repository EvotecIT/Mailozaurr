namespace Mailozaurr;

/// <summary>
/// Represents a reusable compose identity/profile.
/// </summary>
public sealed class MailComposeProfile {
    /// <summary>
    /// Gets or sets the stable identifier for the profile.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the user-facing profile name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the from address for the profile.
    /// </summary>
    public string? From { get; set; }

    /// <summary>
    /// Gets or sets the reply-to address for the profile.
    /// </summary>
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the signature text for the profile.
    /// </summary>
    public string? SignatureText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this profile is the default.
    /// </summary>
    public bool IsDefault { get; set; }
}