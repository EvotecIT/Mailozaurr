using MailKit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Executes IMAP delete operations with per-item results and optional expunge.
/// </summary>
public static class ImapDeleteOperations {
    /// <summary>
    /// Abstraction over IMAP delete operations for testability.
    /// </summary>
    public interface IImapDeleteFolder : ImapBulkFlagOperations.IImapFolder {
        /// <summary>Folder full name.</summary>
        string FullName { get; }

        /// <summary>Expunges messages flagged as deleted.</summary>
        Task ExpungeAsync(CancellationToken cancellationToken = default);
    }

    private sealed class MailKitDeleteFolderAdapter : IImapDeleteFolder {
        private readonly IMailFolder _folder;

        internal MailKitDeleteFolderAdapter(IMailFolder folder) {
            _folder = folder ?? throw new ArgumentNullException(nameof(folder));
        }

        public string FullName => _folder.FullName;

        public Task AddFlagsAsync(IReadOnlyCollection<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) =>
            _folder.AddFlagsAsync(new List<UniqueId>(uids), flags, silent, cancellationToken);

        public Task RemoveFlagsAsync(IReadOnlyCollection<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) =>
            _folder.RemoveFlagsAsync(new List<UniqueId>(uids), flags, silent, cancellationToken);

        public Task AddFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) =>
            _folder.AddFlagsAsync(uid, flags, silent, cancellationToken);

        public Task RemoveFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) =>
            _folder.RemoveFlagsAsync(uid, flags, silent, cancellationToken);

        public Task ExpungeAsync(CancellationToken cancellationToken = default) =>
            _folder.ExpungeAsync(cancellationToken);
    }

    /// <summary>
    /// Per-message delete result.
    /// </summary>
    public sealed class ImapDeleteResultItem {
        /// <summary>Message UID.</summary>
        public UniqueId Uid { get; set; }

        /// <summary>True when delete flag update succeeded.</summary>
        public bool Ok { get; set; }

        /// <summary>Error text when <see cref="Ok"/> is false.</summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Aggregate delete result.
    /// </summary>
    public sealed class ImapDeleteResult {
        /// <summary>Resolved folder full name.</summary>
        public string? Folder { get; set; }

        /// <summary>Total unique UIDs requested.</summary>
        public int Requested { get; set; }

        /// <summary>Count of successful delete flag updates.</summary>
        public int Deleted { get; set; }

        /// <summary>True when expunge was executed.</summary>
        public bool Expunged { get; set; }

        /// <summary>Per-item results in request order.</summary>
        public List<ImapDeleteResultItem> Results { get; set; } = new();

        /// <summary>UIDs successfully marked deleted.</summary>
        public List<UniqueId> DeletedUids { get; set; } = new();
    }

    /// <summary>
    /// Marks messages deleted and optionally expunges the folder.
    /// </summary>
    public static Task<ImapDeleteResult> DeleteAsync(
        IMailFolder folder,
        IReadOnlyCollection<UniqueId> uids,
        bool expunge,
        Func<string, string>? sanitizeError = null,
        CancellationToken cancellationToken = default) {
        if (folder == null) {
            throw new ArgumentNullException(nameof(folder));
        }

        return DeleteAsync(new MailKitDeleteFolderAdapter(folder), uids, expunge, sanitizeError, cancellationToken);
    }

    /// <summary>
    /// Marks messages deleted and optionally expunges the folder.
    /// </summary>
    public static async Task<ImapDeleteResult> DeleteAsync(
        IImapDeleteFolder folder,
        IReadOnlyCollection<UniqueId> uids,
        bool expunge,
        Func<string, string>? sanitizeError = null,
        CancellationToken cancellationToken = default) {
        if (folder == null) {
            throw new ArgumentNullException(nameof(folder));
        }
        if (uids == null) {
            throw new ArgumentNullException(nameof(uids));
        }

        var operation = await ImapBulkFlagOperations.SetFlagsAsync(
            folder,
            uids,
            MessageFlags.Deleted,
            add: true,
            sanitizeError: sanitizeError,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var result = new ImapDeleteResult {
            Folder = folder.FullName,
            Requested = operation.Requested,
            Deleted = operation.Updated,
            Results = new List<ImapDeleteResultItem>(operation.Results.Count),
            DeletedUids = new List<UniqueId>(operation.SuccessfulUids.Count)
        };

        foreach (var item in operation.Results) {
            result.Results.Add(new ImapDeleteResultItem {
                Uid = item.Uid,
                Ok = item.Ok,
                Error = item.Error
            });
        }

        result.DeletedUids.AddRange(operation.SuccessfulUids);

        if (expunge && result.Deleted > 0) {
            await folder.ExpungeAsync(cancellationToken).ConfigureAwait(false);
            result.Expunged = true;
        }

        return result;
    }
}