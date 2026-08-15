using MailKit.Net.Imap;

namespace Mailozaurr.Hosting;

/// <summary>
/// Creates authenticated IMAP sessions from stored profiles.
/// </summary>
public interface IImapSessionFactory {
    /// <summary>
    /// Connects an authenticated IMAP client for the provided profile.
    /// </summary>
    Task<ImapClient> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default);
}