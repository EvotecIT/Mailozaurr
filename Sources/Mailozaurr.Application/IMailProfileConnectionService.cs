namespace Mailozaurr.Application;

/// <summary>
/// Tests whether saved profiles can establish a live provider connection.
/// </summary>
public interface IMailProfileConnectionService {
    /// <summary>Runs a connection test for a saved profile.</summary>
    Task<MailProfileConnectionTestResult> TestAsync(
        string profileId,
        MailProfileConnectionTestScope scope = MailProfileConnectionTestScope.Auto,
        CancellationToken cancellationToken = default);
}
