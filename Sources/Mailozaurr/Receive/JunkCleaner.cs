using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Helper methods for clearing junk mail folders.
/// </summary>
public static class JunkCleaner {
    /// <summary>
    /// Deletes all messages from the specified junk folder.
    /// </summary>
    /// <param name="client">Connected IMAP client.</param>
    /// <param name="folder">Folder name or <c>null</c> for "Junk".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task ClearImapJunkAsync(
        ImapClient client,
        string? folder = null,
        CancellationToken cancellationToken = default) {
        var junk = client.GetCachedFolder(folder ?? "Junk", FolderAccess.ReadWrite);
        var uids = await junk.SearchAsync(SearchQuery.All, cancellationToken).ConfigureAwait(false);
        if (uids.Count == 0) {
            return;
        }
        await junk.AddFlagsAsync(uids, MessageFlags.Deleted, true, cancellationToken).ConfigureAwait(false);
        await junk.ExpungeAsync(cancellationToken).ConfigureAwait(false);
    }
}
