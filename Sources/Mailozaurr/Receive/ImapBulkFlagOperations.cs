using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;

namespace Mailozaurr;

/// <summary>
/// Executes bulk IMAP flag updates with per-item fallback when server bulk operations fail.
/// </summary>
public static class ImapBulkFlagOperations {
    /// <summary>
    /// Abstraction over IMAP flag operations for testability.
    /// </summary>
    public interface IImapFolder {
        /// <summary>Adds flags for a batch of UIDs.</summary>
        Task AddFlagsAsync(IReadOnlyCollection<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default);

        /// <summary>Removes flags for a batch of UIDs.</summary>
        Task RemoveFlagsAsync(IReadOnlyCollection<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default);

        /// <summary>Adds flags for one UID.</summary>
        Task AddFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default);

        /// <summary>Removes flags for one UID.</summary>
        Task RemoveFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default);
    }

    private sealed class MailKitFolderAdapter : IImapFolder {
        private readonly IMailFolder _folder;

        internal MailKitFolderAdapter(IMailFolder folder) {
            _folder = folder ?? throw new ArgumentNullException(nameof(folder));
        }

        public Task AddFlagsAsync(IReadOnlyCollection<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) =>
            _folder.AddFlagsAsync(new List<UniqueId>(uids), flags, silent, cancellationToken);

        public Task RemoveFlagsAsync(IReadOnlyCollection<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) =>
            _folder.RemoveFlagsAsync(new List<UniqueId>(uids), flags, silent, cancellationToken);

        public Task AddFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) =>
            _folder.AddFlagsAsync(uid, flags, silent, cancellationToken);

        public Task RemoveFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) =>
            _folder.RemoveFlagsAsync(uid, flags, silent, cancellationToken);
    }

    /// <summary>
    /// One bulk-flag item result.
    /// </summary>
    public sealed class ImapBulkFlagResultItem {
        /// <summary>Message UID.</summary>
        public UniqueId Uid { get; set; }

        /// <summary>True when update succeeded.</summary>
        public bool Ok { get; set; }

        /// <summary>Error text for failed updates.</summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Bulk-flag operation result.
    /// </summary>
    public sealed class ImapBulkFlagResult {
        /// <summary>Total unique UIDs requested.</summary>
        public int Requested { get; set; }

        /// <summary>Count of successful updates.</summary>
        public int Updated { get; set; }

        /// <summary>Per-item results in request order.</summary>
        public List<ImapBulkFlagResultItem> Results { get; set; } = new();

        /// <summary>UIDs successfully updated.</summary>
        public List<UniqueId> SuccessfulUids { get; set; } = new();
    }

    /// <summary>
    /// Sets or clears IMAP flags for many UIDs with per-item fallback when bulk call fails.
    /// </summary>
    /// <param name="folder">Opened IMAP folder with write access.</param>
    /// <param name="uids">Message UIDs to update.</param>
    /// <param name="flags">Flags to set/clear.</param>
    /// <param name="add">True to add flags, false to remove flags.</param>
    /// <param name="sanitizeError">Optional error sanitizer for per-item failures.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Bulk operation result.</returns>
    public static async Task<ImapBulkFlagResult> SetFlagsAsync(
        IMailFolder folder,
        IReadOnlyCollection<UniqueId> uids,
        MessageFlags flags,
        bool add,
        Func<string, string>? sanitizeError = null,
        CancellationToken cancellationToken = default) {
        if (folder == null) {
            throw new ArgumentNullException(nameof(folder));
        }

        return await SetFlagsAsync(
            new MailKitFolderAdapter(folder),
            uids,
            flags,
            add,
            sanitizeError,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets or clears IMAP flags for many UIDs with per-item fallback when bulk call fails.
    /// </summary>
    public static async Task<ImapBulkFlagResult> SetFlagsAsync(
        IImapFolder folder,
        IReadOnlyCollection<UniqueId> uids,
        MessageFlags flags,
        bool add,
        Func<string, string>? sanitizeError = null,
        CancellationToken cancellationToken = default) {
        if (folder == null) {
            throw new ArgumentNullException(nameof(folder));
        }
        if (uids == null) {
            throw new ArgumentNullException(nameof(uids));
        }

        var unique = Deduplicate(uids);
        var result = new ImapBulkFlagResult {
            Requested = unique.Count,
            Results = new List<ImapBulkFlagResultItem>(unique.Count),
            SuccessfulUids = new List<UniqueId>(unique.Count)
        };
        if (unique.Count == 0) {
            return result;
        }

        try {
            if (add) {
                await folder.AddFlagsAsync(unique, flags, silent: true, cancellationToken).ConfigureAwait(false);
            } else {
                await folder.RemoveFlagsAsync(unique, flags, silent: true, cancellationToken).ConfigureAwait(false);
            }

            foreach (var uid in unique) {
                result.Updated++;
                result.SuccessfulUids.Add(uid);
                result.Results.Add(new ImapBulkFlagResultItem { Uid = uid, Ok = true });
            }
            return result;
        } catch (OperationCanceledException) {
            throw;
        } catch {
            // Fallback path handled below.
        }

        foreach (var uid in unique) {
            try {
                if (add) {
                    await folder.AddFlagsAsync(uid, flags, silent: true, cancellationToken).ConfigureAwait(false);
                } else {
                    await folder.RemoveFlagsAsync(uid, flags, silent: true, cancellationToken).ConfigureAwait(false);
                }

                result.Updated++;
                result.SuccessfulUids.Add(uid);
                result.Results.Add(new ImapBulkFlagResultItem { Uid = uid, Ok = true });
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                var error = ex.Message;
                if (sanitizeError != null) {
                    try {
                        error = sanitizeError(error);
                    } catch {
                        // Ignore sanitizer failures.
                    }
                }
                result.Results.Add(new ImapBulkFlagResultItem { Uid = uid, Ok = false, Error = error });
            }
        }

        return result;
    }

    private static List<UniqueId> Deduplicate(IReadOnlyCollection<UniqueId> uids) {
        var output = new List<UniqueId>(uids.Count);
        var seen = new HashSet<uint>();
        foreach (var uid in uids) {
            var id = uid.Id;
            if (id == 0 || !seen.Add(id)) {
                continue;
            }
            output.Add(uid);
        }
        return output;
    }
}
