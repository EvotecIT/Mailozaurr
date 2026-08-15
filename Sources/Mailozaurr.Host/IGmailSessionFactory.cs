namespace Mailozaurr.Hosting;

/// <summary>
/// Creates authenticated Gmail API sessions from reusable profile definitions.
/// </summary>
public interface IGmailSessionFactory {
    /// <summary>Creates an authenticated Gmail session.</summary>
    Task<GmailSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default);
}