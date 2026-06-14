namespace Mailozaurr.Application;

/// <summary>
/// Well-known authentication flow names persisted with reusable profiles.
/// </summary>
public static class MailProfileAuthFlowNames {
    /// <summary>Interactive user login backed by provider token cache.</summary>
    public const string Interactive = "interactive";

    /// <summary>Manually supplied access token.</summary>
    public const string ManualToken = "manualToken";
}