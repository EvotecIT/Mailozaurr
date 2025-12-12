using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Provides helper methods for removing messages from IMAP or POP3 clients.
/// </summary>
public static class MessageRemover {
    /// <summary>
    /// Deletes a single message from an IMAP folder.
    /// </summary>
    /// <param name="client">Connected IMAP client.</param>
    /// <param name="uid">Unique identifier of the message.</param>
    /// <param name="folder">Optional folder, defaults to Inbox.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task DeleteAsync(
        ImapClient client,
        UniqueId uid,
        string? folder = null,
        CancellationToken cancellationToken = default) =>
        DeleteAsync(client, uid, dryRun: false, folder, cancellationToken);

    /// <summary>
    /// Deletes a single message from an IMAP folder, optionally simulating the change.
    /// </summary>
    public static async Task DeleteAsync(
        ImapClient client,
        UniqueId uid,
        bool dryRun,
        string? folder = null,
        CancellationToken cancellationToken = default) {
        if (dryRun) {
            return;
        }
        var mailFolder = client.GetCachedFolder(folder, FolderAccess.ReadWrite);
        await mailFolder.AddFlagsAsync(uid, MessageFlags.Deleted, true, cancellationToken).ConfigureAwait(false);
        await mailFolder.ExpungeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes multiple messages from an IMAP folder.
    /// </summary>
    /// <param name="client">Connected IMAP client.</param>
    /// <param name="uids">Collection of message identifiers.</param>
    /// <param name="folder">Optional folder, defaults to Inbox.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task DeleteAsync(
        ImapClient client,
        IEnumerable<UniqueId> uids,
        string? folder = null,
        CancellationToken cancellationToken = default) =>
        DeleteAsync(client, uids, dryRun: false, folder, cancellationToken);

    /// <summary>
    /// Deletes multiple messages from an IMAP folder, optionally simulating the change.
    /// </summary>
    public static async Task DeleteAsync(
        ImapClient client,
        IEnumerable<UniqueId> uids,
        bool dryRun,
        string? folder = null,
        CancellationToken cancellationToken = default) {
        if (dryRun) {
            return;
        }
        var list = new List<UniqueId>(uids);
        if (list.Count == 0) {
            return;
        }
        var mailFolder = client.GetCachedFolder(folder, FolderAccess.ReadWrite);
        await mailFolder.AddFlagsAsync(list, MessageFlags.Deleted, true, cancellationToken).ConfigureAwait(false);
        await mailFolder.ExpungeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a single message from a POP3 mailbox.
    /// </summary>
    /// <param name="client">Connected POP3 client.</param>
    /// <param name="index">Index of the message to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task DeleteAsync(
        Pop3Client client,
        int index,
        CancellationToken cancellationToken = default) =>
        DeleteAsync(client, index, dryRun: false, cancellationToken);

    /// <summary>
    /// Deletes a single message from a POP3 mailbox, optionally simulating the change.
    /// </summary>
    public static async Task DeleteAsync(
        Pop3Client client,
        int index,
        bool dryRun,
        CancellationToken cancellationToken = default) {
        if (dryRun) {
            return;
        }
        await client.DeleteMessageAsync(index, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes multiple messages from a POP3 mailbox.
    /// </summary>
    /// <param name="client">Connected POP3 client.</param>
    /// <param name="indexes">Indexes of messages to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task DeleteAsync(
        Pop3Client client,
        IEnumerable<int> indexes,
        CancellationToken cancellationToken = default) =>
        DeleteAsync(client, indexes, dryRun: false, cancellationToken);

    /// <summary>
    /// Deletes multiple messages from a POP3 mailbox, optionally simulating the change.
    /// </summary>
    public static async Task DeleteAsync(
        Pop3Client client,
        IEnumerable<int> indexes,
        bool dryRun,
        CancellationToken cancellationToken = default) {
        if (dryRun) {
            return;
        }
        foreach (var i in indexes) {
            await client.DeleteMessageAsync(i, cancellationToken).ConfigureAwait(false);
        }
    }
}
