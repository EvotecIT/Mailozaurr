namespace Mailozaurr.Hosting;

/// <summary>
/// Creates authenticated Graph API sessions from reusable profile definitions.
/// </summary>
public interface IGraphSessionFactory {
    /// <summary>Creates an authenticated Graph session.</summary>
    Task<GraphSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default);
}