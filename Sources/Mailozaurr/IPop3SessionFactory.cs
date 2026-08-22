using MailKit.Net.Pop3;

namespace Mailozaurr;

/// <summary>
/// Creates authenticated POP3 sessions from stored profiles.
/// </summary>
public interface IPop3SessionFactory {
    /// <summary>Connects an authenticated POP3 client for the provided profile.</summary>
    Task<Pop3Client> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default);
}
