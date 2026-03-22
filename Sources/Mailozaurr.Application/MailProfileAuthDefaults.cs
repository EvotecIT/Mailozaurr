namespace Mailozaurr.Application;

/// <summary>
/// Shared defaults used by profile-based authentication flows.
/// </summary>
public static class MailProfileAuthDefaults {
    /// <summary>Default Gmail mail scope.</summary>
    public static readonly IReadOnlyList<string> GmailScopes = new[] { "https://mail.google.com/" };

    /// <summary>Default Microsoft Graph mail scopes.</summary>
    public static readonly IReadOnlyList<string> GraphScopes = new[] {
        "email",
        "offline_access",
        "https://graph.microsoft.com/Mail.ReadWrite",
        "https://graph.microsoft.com/Mail.Send"
    };

    /// <summary>Default redirect URI used by native Microsoft identity flows.</summary>
    public const string GraphRedirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient";

    /// <summary>Refresh window used when deciding whether a token should be reacquired.</summary>
    public static readonly TimeSpan TokenRefreshWindow = TimeSpan.FromMinutes(5);
}
