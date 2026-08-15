using Mailozaurr;

namespace Mailozaurr.Hosting;

/// <summary>
/// Creates authenticated SMTP sessions from reusable profile definitions.
/// </summary>
public interface ISmtpSessionFactory {
    /// <summary>Creates an authenticated SMTP session.</summary>
    Task<Smtp> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default);
}