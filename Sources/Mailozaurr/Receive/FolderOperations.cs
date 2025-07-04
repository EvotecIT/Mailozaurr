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
    /// Moves a folder to a new parent folder.
    /// </summary>
    public static async Task MoveFolderAsync(
        ImapClient client,
        string sourceFolder,
        string destinationFolder,
        CancellationToken cancellationToken = default) {
        var source = client.GetCachedFolder(sourceFolder, FolderAccess.ReadWrite);
        var destParent = client.GetCachedFolder(destinationFolder, FolderAccess.ReadWrite);
        await source.RenameAsync(destParent, source.Name, cancellationToken).ConfigureAwait(false);
        client.ClearFolderCache();
    }

    /// <summary>
    /// Renames an existing folder.
    /// </summary>
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
    /// Removes a folder.
    /// </summary>
    public static async Task RemoveFolderAsync(
        ImapClient client,
        string folder,
        CancellationToken cancellationToken = default) {
        var src = client.GetCachedFolder(folder, FolderAccess.ReadWrite);
        await src.DeleteAsync(cancellationToken).ConfigureAwait(false);
        client.ClearFolderCache();
    }
}
