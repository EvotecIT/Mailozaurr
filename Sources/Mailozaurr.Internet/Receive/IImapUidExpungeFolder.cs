using MailKit;

namespace Mailozaurr;

/// <summary>Optional folder capability for expunging selected UIDs without deleting unrelated flagged messages.</summary>
public interface IImapUidExpungeFolder {
    /// <summary>Expunges the specified deleted UIDs; returns false if the server does not support UID-scoped expunge.</summary>
    Task<bool> ExpungeAsync(IReadOnlyCollection<UniqueId> uids, CancellationToken cancellationToken = default);
}
