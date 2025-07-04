using MailKit;
using MailKit.Net.Imap;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Provides helper methods for common IMAP folder operations.
/// </summary>
public static class FolderOperations {
    /// <summary>
    /// Moves an IMAP folder to a different parent folder.
    /// </summary>
    /// <param name="client">Active IMAP client instance.</param>
    /// <param name="sourceFolder">Name of the folder to move.</param>
    /// <param name="destinationFolder">Name of the destination folder. Pass <c>null</c> or an empty string to move to the root.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task MoveFolderAsync(
        ImapClient client,
        string sourceFolder,
        string? destinationFolder,
        CancellationToken cancellationToken = default) {
        var source = client.GetCachedFolder(sourceFolder, FolderAccess.ReadWrite);
        IMailFolder destParent;
        if (string.IsNullOrWhiteSpace(destinationFolder)) {
            destParent = client.PersonalNamespaces.Count > 0
                ? client.GetFolder(client.PersonalNamespaces[0])
                : client.GetFolder(string.Empty);
            if (!destParent.IsOpen)
                destParent.Open(FolderAccess.ReadWrite);
        } else {
            destParent = client.GetCachedFolder(destinationFolder, FolderAccess.ReadWrite);
        }
        await source.RenameAsync(destParent, source.Name, cancellationToken).ConfigureAwait(false);
        client.ClearFolderCache();
    }

    /// <summary>
    /// Renames an existing IMAP folder.
    /// </summary>
    /// <param name="client">Active IMAP client instance.</param>
    /// <param name="folder">Name of the folder to rename.</param>
    /// <param name="newName">New name for the folder.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RenameFolderAsync(
        ImapClient client,
        string folder,
        string newName,
        CancellationToken cancellationToken = default) {
        var src = client.GetCachedFolder(folder, FolderAccess.ReadWrite);
        await src.RenameAsync(src.ParentFolder, newName, cancellationToken).ConfigureAwait(false);
        client.ClearFolderCache();
    }

    /// <summary>
    /// Permanently deletes a folder.
    /// </summary>
    /// <param name="client">Active IMAP client instance.</param>
    /// <param name="folder">Name of the folder to remove.</param>
    /// <param name="recursive">When set to <c>true</c>, removes all subfolders recursively.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RemoveFolderAsync(
        ImapClient client,
        string folder,
        bool recursive = false,
        CancellationToken cancellationToken = default) {
        var src = client.GetCachedFolder(folder, FolderAccess.ReadWrite);
        if (recursive) {
            foreach (var sub in await src.GetSubfoldersAsync(false, cancellationToken).ConfigureAwait(false)) {
                await RemoveFolderAsync(client, sub.FullName, true, cancellationToken).ConfigureAwait(false);
            }
            src = client.GetCachedFolder(folder, FolderAccess.ReadWrite);
        }
        await src.DeleteAsync(cancellationToken).ConfigureAwait(false);
        client.ClearFolderCache();
    }
}
