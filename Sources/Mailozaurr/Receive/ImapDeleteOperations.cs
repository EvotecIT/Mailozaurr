#pragma warning disable CS1591
#pragma warning disable CS8600,CS8601,CS8602,CS8603,CS8604,CS8618,CS8625
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;

namespace Mailozaurr;

public static class ImapDeleteOperations {
    public sealed class ImapDeleteResult {
        public string Folder { get; init; } = string.Empty;
        public int Requested { get; init; }
        public int Deleted { get; set; }
        public bool Expunged { get; set; }
        public List<UniqueId> DeletedUids { get; init; } = new();
        public List<ImapDeleteResultItem> Results { get; init; } = new();
    }

    public sealed class ImapDeleteResultItem {
        public UniqueId Uid { get; init; }
        public bool Ok { get; init; }
        public string? Error { get; init; }
    }

    public static async Task<ImapDeleteResult> DeleteAsync(
        IMailFolder folder,
        IReadOnlyCollection<UniqueId> uids,
        bool expunge,
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

        var result = new ImapDeleteResult {
            Folder = folder.FullName,
            Requested = uids.Count,
            Expunged = false
        };

        var uidList = new List<UniqueId>(uids.Count);
        uidList.AddRange(uids);

        try {
            await folder.AddFlagsAsync(uidList, MessageFlags.Deleted, silent: true, cancellationToken).ConfigureAwait(false);
            foreach (var uid in uidList) {
                result.Deleted++;
                result.DeletedUids.Add(uid);
                result.Results.Add(new ImapDeleteResultItem { Uid = uid, Ok = true });
            }
        } catch {
            foreach (var uid in uidList) {
                try {
                    await folder.AddFlagsAsync(uid, MessageFlags.Deleted, silent: true, cancellationToken).ConfigureAwait(false);
                    result.Deleted++;
                    result.DeletedUids.Add(uid);
                    result.Results.Add(new ImapDeleteResultItem { Uid = uid, Ok = true });
                } catch (Exception ex) {
                    result.Results.Add(new ImapDeleteResultItem {
                        Uid = uid,
                        Ok = false,
                        Error = sanitizeError(ex.Message)
                    });
                }
            }
        }

        if (expunge && result.Deleted > 0) {
            await folder.ExpungeAsync(cancellationToken).ConfigureAwait(false);
            result.Expunged = true;
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
