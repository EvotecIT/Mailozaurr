using MailKit;

namespace Mailozaurr;

/// <summary>Expunges only selected UIDs and never broadens a targeted operation to the entire folder.</summary>
internal static class ImapExpunge {
    internal static Task<bool> SelectedAsync(object folder, IReadOnlyCollection<UniqueId> uids, CancellationToken cancellationToken) =>
        folder is IImapUidExpungeFolder targeted ? targeted.ExpungeAsync(uids, cancellationToken) : Task.FromResult(false);

    internal static async Task<bool> SelectedAsync(IMailFolder folder, IList<UniqueId> uids, CancellationToken cancellationToken) {
        if (uids.Count == 0) return false;
        try {
            await folder.ExpungeAsync(uids, cancellationToken).ConfigureAwait(false);
            return true;
        } catch (NotSupportedException) {
            // Without UIDPLUS the selected messages remain flagged. A global
            // EXPUNGE would also permanently remove another client's messages.
            return false;
        }
    }
}
