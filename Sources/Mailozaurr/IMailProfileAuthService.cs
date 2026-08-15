namespace Mailozaurr;

/// <summary>
/// Performs reusable profile authentication flows and persists resulting credentials.
/// </summary>
public interface IMailProfileAuthService {
    /// <summary>Returns the current persisted authentication status for a saved profile.</summary>
    Task<MailProfileAuthStatus?> GetStatusAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>Authenticates a saved Gmail profile and persists the resulting tokens.</summary>
    Task<MailProfileAuthenticationResult> LoginGmailAsync(GmailProfileLoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Authenticates a saved Microsoft Graph profile and persists the resulting access token.</summary>
    Task<MailProfileAuthenticationResult> LoginGraphAsync(GraphProfileLoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Refreshes or reauthenticates a saved profile using its persisted metadata.</summary>
    Task<MailProfileAuthenticationResult> RefreshAsync(string profileId, CancellationToken cancellationToken = default);
}