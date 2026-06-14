using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Executes IMAP move operations with portable fallback for servers without MOVE support.
/// </summary>
public static class ImapMoveOperations {
    /// <summary>
    /// Abstraction over IMAP folder move operations for testability.
    /// </summary>
    public interface IImapMoveFolder {
        /// <summary>Folder full name.</summary>
        string FullName { get; }

        /// <summary>True when folder is open.</summary>
        bool IsOpen { get; }

        /// <summary>Current folder access when opened.</summary>
        FolderAccess Access { get; }

        /// <summary>Opens the folder with requested access.</summary>
        Task OpenAsync(FolderAccess access, CancellationToken cancellationToken = default);

        /// <summary>Closes the folder.</summary>
        Task CloseAsync(bool expunge, CancellationToken cancellationToken = default);

        /// <summary>Fetches raw Message-Id header value for a message UID.</summary>
        Task<string?> GetMessageIdHeaderAsync(UniqueId uid, CancellationToken cancellationToken = default);

        /// <summary>Moves one UID to destination folder.</summary>
        Task MoveToAsync(UniqueId uid, IImapMoveFolder destination, CancellationToken cancellationToken = default);

        /// <summary>Copies UIDs to destination and returns UID mapping when available.</summary>
        Task<IDictionary<UniqueId, UniqueId>?> CopyToWithMapAsync(IReadOnlyCollection<UniqueId> uids, IImapMoveFolder destination, CancellationToken cancellationToken = default);

        /// <summary>Copies one UID to destination folder.</summary>
        Task CopyToAsync(UniqueId uid, IImapMoveFolder destination, CancellationToken cancellationToken = default);

        /// <summary>Adds flags to one UID.</summary>
        Task AddFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default);

        /// <summary>Expunges the folder.</summary>
        Task ExpungeAsync(CancellationToken cancellationToken = default);

        /// <summary>Searches destination for Message-Id matches.</summary>
        Task<IList<UniqueId>> SearchByMessageIdAsync(string messageId, CancellationToken cancellationToken = default);
    }

    private sealed class MailKitMoveFolderAdapter : IImapMoveFolder {
        private readonly IMailFolder _folder;

        internal MailKitMoveFolderAdapter(IMailFolder folder) {
            _folder = folder ?? throw new ArgumentNullException(nameof(folder));
        }

        public string FullName => _folder.FullName;

        public bool IsOpen => _folder.IsOpen;

        public FolderAccess Access => _folder.Access;

        public Task OpenAsync(FolderAccess access, CancellationToken cancellationToken = default) =>
            _folder.OpenAsync(access, cancellationToken);

        public Task CloseAsync(bool expunge, CancellationToken cancellationToken = default) =>
            _folder.CloseAsync(expunge, cancellationToken);

        public async Task<string?> GetMessageIdHeaderAsync(UniqueId uid, CancellationToken cancellationToken = default) {
            var headers = await _folder.GetHeadersAsync(uid, cancellationToken).ConfigureAwait(false);
            return headers[HeaderId.MessageId];
        }

        public Task MoveToAsync(UniqueId uid, IImapMoveFolder destination, CancellationToken cancellationToken = default) =>
            _folder.MoveToAsync(uid, AsMailKitFolder(destination), cancellationToken);

        public async Task<IDictionary<UniqueId, UniqueId>?> CopyToWithMapAsync(IReadOnlyCollection<UniqueId> uids, IImapMoveFolder destination, CancellationToken cancellationToken = default) {
            var map = await _folder.CopyToAsync(new List<UniqueId>(uids), AsMailKitFolder(destination), cancellationToken).ConfigureAwait(false);
            if (map == null) {
                return null;
            }

            var resolved = new Dictionary<UniqueId, UniqueId>(uids.Count);
            foreach (var uid in uids) {
                if (map.TryGetValue(uid, out var mapped)) {
                    resolved[uid] = mapped;
                }
            }
            return resolved;
        }

        public Task CopyToAsync(UniqueId uid, IImapMoveFolder destination, CancellationToken cancellationToken = default) =>
            _folder.CopyToAsync(uid, AsMailKitFolder(destination), cancellationToken);

        public Task AddFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) =>
            _folder.AddFlagsAsync(uid, flags, silent, cancellationToken);

        public Task ExpungeAsync(CancellationToken cancellationToken = default) =>
            _folder.ExpungeAsync(cancellationToken);

        public Task<IList<UniqueId>> SearchByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) =>
            _folder.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId), cancellationToken);

        private static IMailFolder AsMailKitFolder(IImapMoveFolder folder) {
            if (folder is not MailKitMoveFolderAdapter adapter) {
                throw new ArgumentException("folder adapter type is not supported for MailKit operations.", nameof(folder));
            }

            return adapter._folder;
        }
    }

    /// <summary>
    /// Per-message result item for IMAP move operations.
    /// </summary>
    public sealed class ImapMoveResultItem {
        /// <summary>Source message UID.</summary>
        public UniqueId Uid { get; set; }

        /// <summary>True when move succeeded for this UID.</summary>
        public bool Ok { get; set; }

        /// <summary>Destination UID when available.</summary>
        public long? TargetUid { get; set; }

        /// <summary>Normalized Message-Id used for destination lookup.</summary>
        public string? MessageId { get; set; }

        /// <summary>True when MOVE fell back to COPY + DELETE.</summary>
        public bool UsedCopyFallback { get; set; }

        /// <summary>Failure reason when <see cref="Ok"/> is false.</summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Aggregate result for IMAP move operations.
    /// </summary>
    public sealed class ImapMoveResult {
        /// <summary>Resolved source folder full name.</summary>
        public string? SourceFolder { get; set; }

        /// <summary>Resolved target folder full name.</summary>
        public string? TargetFolder { get; set; }

        /// <summary>Total unique UIDs requested.</summary>
        public int Requested { get; set; }

        /// <summary>Count of successful moves.</summary>
        public int Moved { get; set; }

        /// <summary>Per-item results in request order.</summary>
        public List<ImapMoveResultItem> Results { get; set; } = new();
    }

    /// <summary>
    /// Moves one or many UIDs between folders using an IMAP client.
    /// </summary>
    public static async Task<ImapMoveResult> MoveAsync(
        ImapClient client,
        string sourceFolder,
        string targetFolder,
        IReadOnlyCollection<UniqueId> uids,
        Func<string, string>? sanitizeError = null,
        CancellationToken cancellationToken = default) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (string.IsNullOrWhiteSpace(sourceFolder)) {
            throw new ArgumentException("sourceFolder is required.", nameof(sourceFolder));
        }
        if (string.IsNullOrWhiteSpace(targetFolder)) {
            throw new ArgumentException("targetFolder is required.", nameof(targetFolder));
        }

        var source = new MailKitMoveFolderAdapter(client.GetFolder(sourceFolder));
        var destination = new MailKitMoveFolderAdapter(client.GetFolder(targetFolder));
        return await MoveAsync(source, destination, uids, sanitizeError, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves one or many UIDs between folders using abstract folders.
    /// </summary>
    public static async Task<ImapMoveResult> MoveAsync(
        IImapMoveFolder source,
        IImapMoveFolder destination,
        IReadOnlyCollection<UniqueId> uids,
        Func<string, string>? sanitizeError = null,
        CancellationToken cancellationToken = default) {
        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }
        if (destination == null) {
            throw new ArgumentNullException(nameof(destination));
        }
        if (uids == null) {
            throw new ArgumentNullException(nameof(uids));
        }

        var unique = Deduplicate(uids);
        var result = new ImapMoveResult {
            SourceFolder = source.FullName,
            TargetFolder = destination.FullName,
            Requested = unique.Count,
            Results = new List<ImapMoveResultItem>(unique.Count)
        };

        if (unique.Count == 0) {
            return result;
        }

        if (source.FullName.Equals(destination.FullName, StringComparison.OrdinalIgnoreCase)) {
            foreach (var uid in unique) {
                result.Moved++;
                result.Results.Add(new ImapMoveResultItem {
                    Uid = uid,
                    Ok = true,
                    TargetUid = uid.Id,
                    MessageId = null,
                    UsedCopyFallback = false
                });
            }
            return result;
        }

        await EnsureFolderAccessAsync(source, FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
        await EnsureFolderAccessAsync(destination, FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);

        result.SourceFolder = source.FullName;
        result.TargetFolder = destination.FullName;

        var sourceNeedsExpunge = false;
        foreach (var uid in unique) {
            string? messageId = null;
            long? targetUid = null;
            var usedCopyFallback = false;

            try {
                try {
                    var raw = await source.GetMessageIdHeaderAsync(uid, cancellationToken).ConfigureAwait(false);
                    messageId = NormalizeMessageIdHeader(raw);
                } catch {
                    // best-effort
                    messageId = null;
                }

                try {
                    await source.MoveToAsync(uid, destination, cancellationToken).ConfigureAwait(false);
                } catch (NotSupportedException) {
                    usedCopyFallback = true;

                    try {
                        var map = await source.CopyToWithMapAsync(new[] { uid }, destination, cancellationToken).ConfigureAwait(false);
                        if (map != null && map.TryGetValue(uid, out var mapped)) {
                            targetUid = mapped.Id;
                        }
                    } catch {
                        await source.CopyToAsync(uid, destination, cancellationToken).ConfigureAwait(false);
                    }

                    await source.AddFlagsAsync(uid, MessageFlags.Deleted, silent: true, cancellationToken).ConfigureAwait(false);
                    sourceNeedsExpunge = true;
                }

                if (!targetUid.HasValue && !string.IsNullOrWhiteSpace(messageId)) {
                    try {
                        var lookupMessageId = messageId!;
                        var matches = await destination.SearchByMessageIdAsync(lookupMessageId, cancellationToken).ConfigureAwait(false);
                        if (matches.Count > 0) {
                            targetUid = matches[matches.Count - 1].Id;
                        }
                    } catch {
                        // best-effort
                    }
                }

                result.Moved++;
                result.Results.Add(new ImapMoveResultItem {
                    Uid = uid,
                    Ok = true,
                    TargetUid = targetUid,
                    MessageId = messageId,
                    UsedCopyFallback = usedCopyFallback
                });
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

                result.Results.Add(new ImapMoveResultItem {
                    Uid = uid,
                    Ok = false,
                    TargetUid = targetUid,
                    MessageId = messageId,
                    UsedCopyFallback = usedCopyFallback,
                    Error = error
                });
            }
        }

        if (sourceNeedsExpunge) {
            try {
                await source.ExpungeAsync(cancellationToken).ConfigureAwait(false);
            } catch {
                // best-effort: copies already exist in destination.
            }
        }

        return result;
    }

    private static async Task EnsureFolderAccessAsync(IImapMoveFolder folder, FolderAccess access, CancellationToken cancellationToken) {
        if (folder.IsOpen && folder.Access != access) {
            try {
                await folder.CloseAsync(expunge: false, cancellationToken).ConfigureAwait(false);
            } catch {
                // best-effort
            }
        }

        if (!folder.IsOpen || folder.Access != access) {
            await folder.OpenAsync(access, cancellationToken).ConfigureAwait(false);
        }

        if (!folder.IsOpen || folder.Access != access) {
            throw new InvalidOperationException($"Folder is not currently open in {access} mode.");
        }
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

    private static string? NormalizeMessageIdHeader(string? rawHeaderValue) {
        if (string.IsNullOrWhiteSpace(rawHeaderValue)) {
            return null;
        }

        var first = ExtractFirstMessageId(rawHeaderValue);
        return ImapSentMessageOperations.NormalizeMessageIdToken(first);
    }

    private static string? ExtractFirstMessageId(string? raw) {
        if (string.IsNullOrWhiteSpace(raw)) {
            return null;
        }

        var parts = raw!.Split(
            new[] { ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? null : parts[0];
    }
}