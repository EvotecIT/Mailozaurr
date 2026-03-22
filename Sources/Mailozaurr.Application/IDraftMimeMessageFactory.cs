using MimeKit;

namespace Mailozaurr.Application;

/// <summary>
/// Builds reusable MIME messages from normalized draft contracts.
/// </summary>
public interface IDraftMimeMessageFactory {
    /// <summary>
    /// Creates a MIME message for the provided profile and draft.
    /// </summary>
    Task<MimeMessage> CreateAsync(MailProfile profile, DraftMessage draft, CancellationToken cancellationToken = default);
}
