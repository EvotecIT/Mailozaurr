namespace Mailozaurr;

/// <summary>
/// Request used to authenticate an existing Gmail profile.
/// </summary>
public sealed class GmailProfileLoginRequest {
    /// <summary>Stable profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional Gmail account override used for the login flow.</summary>
    public string? GmailAccount { get; set; }

    /// <summary>Optional OAuth client identifier override.</summary>
    public string? ClientId { get; set; }

    /// <summary>Optional OAuth client secret override.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Optional secret reference for the client secret, in the form <c>&lt;profile-id&gt;:&lt;secret-name&gt;</c> or <c>&lt;secret-name&gt;</c>.</summary>
    public string? ClientSecretReference { get; set; }

    /// <summary>Optional scopes override. Defaults to the Mailozaurr Gmail mail scope.</summary>
    public IReadOnlyList<string>? Scopes { get; set; }
}