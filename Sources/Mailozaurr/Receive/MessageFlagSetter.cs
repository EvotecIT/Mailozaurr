using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Provides helper methods for setting message flags on IMAP and POP3 servers.
/// </summary>
public static class MessageFlagSetter {
    /// <summary>
    /// Abstraction for flag operations on a folder.
    /// </summary>
    public interface IImapFolder {
        /// <summary>Adds flags to the specified message.</summary>
        /// <param name="uid">Message unique identifier.</param>
        /// <param name="flags">Flags to add.</param>
        /// <param name="silent">Whether to perform the operation silently.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        Task AddFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default);
        /// <summary>Removes flags from the specified message.</summary>
        /// <param name="uid">Message unique identifier.</param>
        /// <param name="flags">Flags to remove.</param>
        /// <param name="silent">Whether to perform the operation silently.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        Task RemoveFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default);
    }

    private class FolderWrapper(IMailFolder folder) : IImapFolder {
        /// <inheritdoc />
        public Task AddFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) =>
            folder.AddFlagsAsync(uid, flags, silent, cancellationToken);
        /// <inheritdoc />
        public Task RemoveFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) =>
            folder.RemoveFlagsAsync(uid, flags, silent, cancellationToken);
    }

    /// <summary>
    /// Sets or clears message flags in an IMAP folder.
    /// </summary>
    public static Task SetFlagsAsync(ImapClient client, UniqueId uid, MessageFlags flags, bool add, string? folder = null, CancellationToken cancellationToken = default) =>
        SetFlagsAsync(client, uid, flags, add, dryRun: false, folder, cancellationToken);

    /// <summary>
    /// Sets or clears message flags in an IMAP folder, optionally simulating the change.
    /// </summary>
    public static Task SetFlagsAsync(ImapClient client, UniqueId uid, MessageFlags flags, bool add, bool dryRun, string? folder = null, CancellationToken cancellationToken = default) {
        if (dryRun) {
            return Task.CompletedTask;
        }
        var mailFolder = client.GetCachedFolder(folder, FolderAccess.ReadWrite);
        return SetFlagsAsync(new FolderWrapper(mailFolder), uid, flags, add, cancellationToken);
    }

    /// <summary>
    /// Sets or clears message flags using an abstract folder.
    /// </summary>
    public static Task SetFlagsAsync(IImapFolder folder, UniqueId uid, MessageFlags flags, bool add, CancellationToken cancellationToken = default) =>
        SetFlagsAsync(folder, uid, flags, add, dryRun: false, cancellationToken);

    /// <summary>
    /// Sets or clears message flags using an abstract folder, optionally simulating the change.
    /// </summary>
    public static Task SetFlagsAsync(IImapFolder folder, UniqueId uid, MessageFlags flags, bool add, bool dryRun, CancellationToken cancellationToken = default) {
        if (dryRun) {
            return Task.CompletedTask;
        }
        return add ? folder.AddFlagsAsync(uid, flags, true, cancellationToken)
            : folder.RemoveFlagsAsync(uid, flags, true, cancellationToken);
    }

    private static readonly ConditionalWeakTable<Pop3Client, ConcurrentDictionary<int, bool>> Pop3Flags = new();

    /// <summary>
    /// Sets or clears the local read flag for a POP3 message.
    /// </summary>
    public static Task SetReadAsync(Pop3Client client, int index, bool read, CancellationToken cancellationToken = default) =>
        SetReadAsync(client, index, read, dryRun: false, cancellationToken);

    /// <summary>
    /// Sets or clears the local read flag for a POP3 message, optionally simulating the change.
    /// </summary>
    public static Task SetReadAsync(Pop3Client client, int index, bool read, bool dryRun, CancellationToken cancellationToken = default) {
        if (dryRun) {
            return Task.CompletedTask;
        }
        var state = Pop3Flags.GetOrCreateValue(client);
        state[index] = read;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Tries to get the local read flag for a POP3 message.
    /// </summary>
    /// <param name="client">POP3 client.</param>
    /// <param name="index">Message index.</param>
    /// <param name="read">Returns the current read state.</param>
    /// <returns><c>true</c> when a state was found.</returns>
    public static bool TryGetPop3Read(Pop3Client client, int index, out bool read) {
        var state = Pop3Flags.GetOrCreateValue(client);
        return state.TryGetValue(index, out read);
    }
}
