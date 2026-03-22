namespace Mailozaurr.Application;

/// <summary>
/// Represents the result of an authentication flow for a saved profile.
/// </summary>
public sealed class MailProfileAuthenticationResult : OperationResult {
    /// <summary>The profile identifier the login result applies to.</summary>
    public string? ProfileId { get; set; }

    /// <summary>The provider kind associated with the authenticated profile.</summary>
    public MailProfileKind ProfileKind { get; set; } = MailProfileKind.Unknown;

    /// <summary>The resolved user/account name returned by the authentication flow.</summary>
    public string? UserName { get; set; }

    /// <summary>The access-token expiry when known.</summary>
    public DateTimeOffset? ExpiresOn { get; set; }
}
