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
        await MoveFolderAsync(client, sourceFolder, destinationFolder, dryRun: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves an IMAP folder to a different parent folder, optionally simulating the change.
    /// </summary>
    public static async Task MoveFolderAsync(
        ImapClient client,
        string sourceFolder,
        string? destinationFolder,
        bool dryRun,
        CancellationToken cancellationToken = default) {
        if (dryRun) {
            return;
        }
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
        try {
            await source.RenameAsync(destParent, source.Name, cancellationToken).ConfigureAwait(false);
        } finally {
            if (source.IsOpen)
                await source.CloseAsync(false, cancellationToken).ConfigureAwait(false);
            if (destParent.IsOpen)
                await destParent.CloseAsync(false, cancellationToken).ConfigureAwait(false);
            client.ClearFolderCache();
        }
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
        await RenameFolderAsync(client, folder, newName, dryRun: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Renames an existing IMAP folder, optionally simulating the change.
    /// </summary>
    public static async Task RenameFolderAsync(
        ImapClient client,
        string folder,
        string newName,
        bool dryRun,
        CancellationToken cancellationToken = default) {
        if (dryRun) {
            return;
        }
        var src = client.GetCachedFolder(folder, FolderAccess.ReadWrite);
        try {
            var parent = src.ParentFolder;
            if (parent is null) {
                throw new InvalidOperationException($"Cannot rename folder '{folder}' because its parent folder could not be resolved.");
            }

            await src.RenameAsync(parent, newName, cancellationToken).ConfigureAwait(false);
        } finally {
            if (src.IsOpen)
                await src.CloseAsync(false, cancellationToken).ConfigureAwait(false);
            client.ClearFolderCache();
        }
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
        await RemoveFolderAsync(client, folder, recursive, dryRun: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Permanently deletes a folder, optionally simulating the change.
    /// </summary>
    public static async Task RemoveFolderAsync(
        ImapClient client,
        string folder,
        bool recursive,
        bool dryRun,
        CancellationToken cancellationToken = default) {
        if (dryRun) {
            return;
        }
        var src = client.GetCachedFolder(folder, FolderAccess.ReadWrite);
        try {
            if (recursive) {
                foreach (var sub in await src.GetSubfoldersAsync(false, cancellationToken).ConfigureAwait(false))
                    await RemoveFolderAsync(client, sub.FullName, true, dryRun, cancellationToken).ConfigureAwait(false);

                if (src.IsOpen)
                    await src.CloseAsync(false, cancellationToken).ConfigureAwait(false);
                src = client.GetCachedFolder(folder, FolderAccess.ReadWrite);
            }

            await src.DeleteAsync(cancellationToken).ConfigureAwait(false);
        } finally {
            if (src.IsOpen)
                await src.CloseAsync(false, cancellationToken).ConfigureAwait(false);
            client.ClearFolderCache();
        }
    }
}