namespace Mailozaurr.Hosting;

/// <summary>
/// Represents the resolved session input required to connect to Gmail.
/// </summary>
public sealed class GmailSessionRequest {
    /// <summary>Resolved Gmail mailbox user id.</summary>
    public string UserId { get; set; } = "me";

    /// <summary>OAuth credential used to authenticate Gmail requests.</summary>
    public OAuthCredential Credential { get; set; } = new();

    /// <summary>Optional access-token refresh delegate.</summary>
    public Func<CancellationToken, Task<string>>? RefreshAccessTokenAsync { get; set; }
}