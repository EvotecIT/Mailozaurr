#pragma warning disable CS1591
#pragma warning disable CS8600,CS8601,CS8602,CS8603,CS8604,CS8618,CS8625
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

namespace Mailozaurr;

public static class ImapMoveOperations {
    public sealed class ImapMoveResult {
        public string? SourceFolder { get; init; }
        public string? TargetFolder { get; init; }
        public int Requested { get; init; }
        public int Moved { get; set; }
        public List<ImapMoveResultItem> Results { get; init; } = new();
    }

    public sealed class ImapMoveResultItem {
        public UniqueId Uid { get; init; }
        public bool Ok { get; set; }
        public long? TargetUid { get; set; }
        public string? MessageId { get; set; }
        public bool UsedCopyFallback { get; set; }
        public string? Error { get; set; }
    }

    public static async Task<ImapMoveResult> MoveAsync(
        ImapClient client,
        string sourceFolder,
        string targetFolder,
        IReadOnlyCollection<UniqueId> uids,
        Func<string, string>? sanitizeError,
        CancellationToken cancellationToken) {
        if (client is null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (string.IsNullOrWhiteSpace(sourceFolder)) {
            throw new ArgumentException("sourceFolder is required.", nameof(sourceFolder));
        }
        if (string.IsNullOrWhiteSpace(targetFolder)) {
            throw new ArgumentException("targetFolder is required.", nameof(targetFolder));
        }
        if (uids is null) {
            throw new ArgumentNullException(nameof(uids));
        }

        sanitizeError ??= static message => message;

        var source = client.GetFolder(sourceFolder);
        await EnsureFolderAccessAsync(source, FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);

        var destination = client.GetFolder(targetFolder);
        await EnsureFolderAccessAsync(destination, FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);

        var result = new ImapMoveResult {
            SourceFolder = source.FullName,
            TargetFolder = destination.FullName,
            Requested = uids.Count
        };

        var sourceNeedsExpunge = false;
        foreach (var uid in uids) {
            var item = new ImapMoveResultItem { Uid = uid };
            try {
                item.MessageId = await TryGetNormalizedMessageIdAsync(source, uid, cancellationToken).ConfigureAwait(false);

                try {
                    var map = await source.MoveToAsync(new List<UniqueId> { uid }, destination, cancellationToken).ConfigureAwait(false);
                    if (map.TryGetValue(uid, out var mappedUid)) {
                        item.TargetUid = mappedUid.Id;
                    }
                } catch (NotSupportedException) {
                    item.UsedCopyFallback = true;
                    await CopyWithFallbackAsync(source, destination, uid, cancellationToken, item).ConfigureAwait(false);
                    await source.AddFlagsAsync(uid, MessageFlags.Deleted, silent: true, cancellationToken).ConfigureAwait(false);
                    sourceNeedsExpunge = true;
                }

                if (!item.TargetUid.HasValue && !string.IsNullOrWhiteSpace(item.MessageId)) {
                    item.TargetUid = await TryResolveTargetUidByMessageIdAsync(destination, item.MessageId, cancellationToken).ConfigureAwait(false);
                }

                item.Ok = true;
                result.Moved++;
            } catch (Exception ex) {
                item.Ok = false;
                item.Error = sanitizeError(ex.Message);
            }

            result.Results.Add(item);
        }

        if (sourceNeedsExpunge) {
            try {
                await source.ExpungeAsync(cancellationToken).ConfigureAwait(false);
            } catch {
                // best-effort; successful copies were already placed in destination
            }
        }

        return result;
    }

    private static async Task CopyWithFallbackAsync(
        IMailFolder source,
        IMailFolder destination,
        UniqueId uid,
        CancellationToken cancellationToken,
        ImapMoveResultItem item) {
        try {
            var map = await source.CopyToAsync(new List<UniqueId> { uid }, destination, cancellationToken).ConfigureAwait(false);
            if (map.TryGetValue(uid, out var mappedUid)) {
                item.TargetUid = mappedUid.Id;
                return;
            }
        } catch {
            // fall through to copy without mapping
        }

        await source.CopyToAsync(uid, destination, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> TryGetNormalizedMessageIdAsync(IMailFolder source, UniqueId uid, CancellationToken cancellationToken) {
        try {
            var headers = await source.GetHeadersAsync(uid, cancellationToken).ConfigureAwait(false);
            var raw = headers[HeaderId.MessageId];
            return NormalizeMessageId(ExtractFirstToken(raw));
        } catch {
            return null;
        }
    }

    private static async Task<long?> TryResolveTargetUidByMessageIdAsync(IMailFolder destination, string messageId, CancellationToken cancellationToken) {
        try {
            var matches = await destination.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId), cancellationToken).ConfigureAwait(false);
            if (matches.Count == 0) {
                return null;
            }
            return matches[matches.Count - 1].Id;
        } catch {
            return null;
        }
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

    private static string? NormalizeMessageId(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        var trimmed = value.Trim();
        if (trimmed.Length == 0) {
            return null;
        }

        var start = 0;
        var end = trimmed.Length;
        if (trimmed[start] == '<') {
            start++;
        }
        if (end > start && trimmed[end - 1] == '>') {
            end--;
        }

        var normalized = trimmed.Substring(start, end - start).Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? ExtractFirstToken(string? raw) {
        if (string.IsNullOrWhiteSpace(raw)) {
            return null;
        }

        var parts = raw.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts) {
            var token = part.Trim();
            if (token.Length > 0) {
                return token;
            }
        }
        return null;
    }
}
