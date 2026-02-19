#pragma warning disable CS1591
#pragma warning disable CS8600,CS8601,CS8602,CS8603,CS8604,CS8618,CS8625
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;

namespace Mailozaurr;

public static class ImapBulkFlagOperations {
    public sealed class ImapBulkFlagResultItem {
        public UniqueId Uid { get; init; }
        public bool Ok { get; init; }
        public string? Error { get; init; }
    }

    public sealed class ImapBulkFlagResult {
        public int Requested { get; init; }
        public int Updated { get; set; }
        public List<UniqueId> SuccessfulUids { get; init; } = new();
        public List<ImapBulkFlagResultItem> Results { get; init; } = new();
    }

    public static async Task<ImapBulkFlagResult> SetFlagsAsync(
        IMailFolder folder,
        IReadOnlyCollection<UniqueId> uids,
        MessageFlags flags,
        bool add,
        Func<string, string>? sanitizeError,
        CancellationToken cancellationToken) {
        if (folder is null) {
            throw new ArgumentNullException(nameof(folder));
        }
        if (uids is null) {
            throw new ArgumentNullException(nameof(uids));
        }

        sanitizeError ??= static message => message;
        await EnsureFolderAccessAsync(folder, FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);

        var result = new ImapBulkFlagResult { Requested = uids.Count };
        var uidList = new List<UniqueId>(uids.Count);
        uidList.AddRange(uids);

        try {
            if (add) {
                await folder.AddFlagsAsync(uidList, flags, silent: true, cancellationToken).ConfigureAwait(false);
            } else {
                await folder.RemoveFlagsAsync(uidList, flags, silent: true, cancellationToken).ConfigureAwait(false);
            }

            foreach (var uid in uidList) {
                result.Updated++;
                result.SuccessfulUids.Add(uid);
                result.Results.Add(new ImapBulkFlagResultItem { Uid = uid, Ok = true });
            }
        } catch {
            foreach (var uid in uidList) {
                try {
                    if (add) {
                        await folder.AddFlagsAsync(uid, flags, silent: true, cancellationToken).ConfigureAwait(false);
                    } else {
                        await folder.RemoveFlagsAsync(uid, flags, silent: true, cancellationToken).ConfigureAwait(false);
                    }

                    result.Updated++;
                    result.SuccessfulUids.Add(uid);
                    result.Results.Add(new ImapBulkFlagResultItem { Uid = uid, Ok = true });
                } catch (Exception ex) {
                    result.Results.Add(new ImapBulkFlagResultItem {
                        Uid = uid,
                        Ok = false,
                        Error = sanitizeError(ex.Message)
                    });
                }
            }
        }

        return result;
    }

    private static async Task EnsureFolderAccessAsync(IMailFolder folder, FolderAccess access, CancellationToken cancellationToken) {
        if (!folder.IsOpen) {
            await folder.OpenAsync(access, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (folder.Access != access && access == FolderAccess.ReadWrite) {
            await folder.OpenAsync(access, cancellationToken).ConfigureAwait(false);
        }
    }
}
