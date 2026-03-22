namespace Mailozaurr.Application;

/// <summary>
/// Request used to authenticate an existing Microsoft Graph profile.
/// </summary>
public sealed class GraphProfileLoginRequest {
    /// <summary>Stable profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Optional login hint override for the interactive flow.</summary>
    public string? Login { get; set; }

    /// <summary>Optional mailbox override that should be persisted with the profile.</summary>
    public string? Mailbox { get; set; }

    /// <summary>Optional application/client identifier override.</summary>
    public string? ClientId { get; set; }

    /// <summary>Optional tenant/directory identifier override.</summary>
    public string? TenantId { get; set; }

    /// <summary>Optional redirect URI override. Defaults to the Mailozaurr Graph native-client redirect URI.</summary>
    public string? RedirectUri { get; set; }

    /// <summary>Optional scopes override. Defaults to Mail.ReadWrite, Mail.Send, email, and offline_access.</summary>
    public IReadOnlyList<string>? Scopes { get; set; }
}
