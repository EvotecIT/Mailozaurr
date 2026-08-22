using MimeKit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

public sealed partial class GmailMailboxBrowser {
    private async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> ExecuteBatchModifyAsync(
        IEnumerable<string> messageIds,
        IReadOnlyCollection<string> addLabelIds,
        IReadOnlyCollection<string> removeLabelIds,
        int batchSize,
        CancellationToken cancellationToken) {
        var ids = NormalizeIds(messageIds);
        if (ids.Count == 0) {
            return Array.Empty<GmailMailboxBulkOperationResult>();
        }

        var results = new List<GmailMailboxBulkOperationResult>(ids.Count);
        foreach (var chunk in Chunk(ids, ClampInt(batchSize, 1, BatchMaxIds))) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                await _gmail.BatchModifyMessagesAsync(
                    _userId,
                    chunk,
                    addLabelIds: addLabelIds,
                    removeLabelIds: removeLabelIds,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                foreach (var id in chunk) {
                    results.Add(new GmailMailboxBulkOperationResult { Id = id, Ok = true });
                }
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                foreach (var id in chunk) {
                    results.Add(new GmailMailboxBulkOperationResult { Id = id, Ok = false, Error = ex.Message });
                }
            }
        }

        return results;
    }

    private async Task<IReadOnlyList<GmailMailboxBulkOperationResult>> ExecuteThreadActionAsync(
        IEnumerable<string> threadIds,
        Func<string, CancellationToken, Task> actionAsync,
        CancellationToken cancellationToken) {
        if (threadIds == null) {
            throw new ArgumentNullException(nameof(threadIds));
        }
        if (actionAsync == null) {
            throw new ArgumentNullException(nameof(actionAsync));
        }

        var ids = NormalizeIds(threadIds);
        if (ids.Count == 0) {
            return Array.Empty<GmailMailboxBulkOperationResult>();
        }

        var results = new List<GmailMailboxBulkOperationResult>(ids.Count);
        foreach (var id in ids) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                await actionAsync(id, cancellationToken).ConfigureAwait(false);
                results.Add(new GmailMailboxBulkOperationResult { Id = id, Ok = true });
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                results.Add(new GmailMailboxBulkOperationResult { Id = id, Ok = false, Error = ex.Message });
            }
        }

        return results;
    }

    private async Task<string?> ResolveSourceLabelIdAsync(
        string? sourceFolder,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(sourceFolder)) {
            return null;
        }

        return NormalizeOptional(await ResolveLabelIdAsync(sourceFolder, cancellationToken).ConfigureAwait(false));
    }

    private static List<string> NormalizeIds(IEnumerable<string> ids) {
        var output = new List<string>();
        if (ids == null) {
            return output;
        }

        foreach (var raw in ids) {
            var id = NormalizeOptional(raw);
            if (id != null) {
                output.Add(id);
            }
        }

        return output;
    }

    private static IEnumerable<List<string>> Chunk(IReadOnlyList<string> ids, int chunkSize) {
        var safeChunkSize = chunkSize <= 0 ? 1 : chunkSize;
        for (var i = 0; i < ids.Count; i += safeChunkSize) {
            var count = Math.Min(safeChunkSize, ids.Count - i);
            var chunk = new List<string>(count);
            for (var j = 0; j < count; j++) {
                chunk.Add(ids[i + j]);
            }
            yield return chunk;
        }
    }

    private static void AddHistoryRefs(
        IReadOnlyCollection<GmailApiClient.GmailHistoryMessageAdded>? refs,
        HashSet<string> output) {
        if (refs == null || refs.Count == 0) {
            return;
        }

        foreach (var entry in refs) {
            var id = NormalizeOptional(entry?.Message?.Id);
            if (id != null) {
                output.Add(id);
            }
        }
    }

    private static void AddHistoryRefs(
        IReadOnlyCollection<GmailApiClient.GmailHistoryLabelAdded>? refs,
        HashSet<string> output) {
        if (refs == null || refs.Count == 0) {
            return;
        }

        foreach (var entry in refs) {
            var id = NormalizeOptional(entry?.Message?.Id);
            if (id != null) {
                output.Add(id);
            }
        }
    }

    private static void AddHistoryRefs(
        IReadOnlyCollection<GmailApiClient.GmailHistoryMessageDeleted>? refs,
        HashSet<string> output) {
        if (refs == null || refs.Count == 0) {
            return;
        }

        foreach (var entry in refs) {
            var id = NormalizeOptional(entry?.Message?.Id);
            if (id != null) {
                output.Add(id);
            }
        }
    }

    private static void AddHistoryRefs(
        IReadOnlyCollection<GmailApiClient.GmailHistoryLabelRemoved>? refs,
        HashSet<string> output) {
        if (refs == null || refs.Count == 0) {
            return;
        }

        foreach (var entry in refs) {
            var id = NormalizeOptional(entry?.Message?.Id);
            if (id != null) {
                output.Add(id);
            }
        }
    }

    private static void ApplyLabelRemovedHistoryRefs(
        IReadOnlyCollection<GmailApiClient.GmailHistoryLabelRemoved>? refs,
        string resolvedLabelId,
        Dictionary<string, bool> finalStates) {
        if (refs == null || refs.Count == 0) {
            return;
        }

        foreach (var entry in refs) {
            var id = NormalizeOptional(entry?.Message?.Id);
            if (id == null) {
                continue;
            }
            var selectedFolderWasRemoved = entry!.LabelIds?.Any(labelId =>
                string.Equals(NormalizeOptional(labelId), resolvedLabelId, StringComparison.OrdinalIgnoreCase)) == true;
            finalStates[id] = selectedFolderWasRemoved;
        }
    }

    private async Task<GmailMailboxMessageSummary?> TryGetMessageSummaryAsync(string messageId, CancellationToken cancellationToken) {
        try {
            return await GetMessageSummaryAsync(messageId, cancellationToken).ConfigureAwait(false);
        } catch {
            return null;
        }
    }

    private static bool TryResolveSystemLabel(string raw, out string labelId) {
        var normalized = raw.Trim();
        if (normalized.Equals("INBOX", StringComparison.OrdinalIgnoreCase)) {
            labelId = "INBOX";
            return true;
        }
        if (normalized.Equals("SENT", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("SENTITEMS", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("SENT ITEMS", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("SENT MAIL", StringComparison.OrdinalIgnoreCase)) {
            labelId = "SENT";
            return true;
        }
        if (normalized.Equals("TRASH", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("DELETED", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("DELETED ITEMS", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("DELETEDITEMS", StringComparison.OrdinalIgnoreCase)) {
            labelId = "TRASH";
            return true;
        }
        if (normalized.Equals("DRAFT", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("DRAFTS", StringComparison.OrdinalIgnoreCase)) {
            labelId = "DRAFT";
            return true;
        }
        if (normalized.Equals("SPAM", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("JUNK", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("JUNK EMAIL", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("JUNKEMAIL", StringComparison.OrdinalIgnoreCase)) {
            labelId = "SPAM";
            return true;
        }
        if (normalized.Equals("STARRED", StringComparison.OrdinalIgnoreCase)) {
            labelId = "STARRED";
            return true;
        }
        if (normalized.Equals("IMPORTANT", StringComparison.OrdinalIgnoreCase)) {
            labelId = "IMPORTANT";
            return true;
        }

        labelId = string.Empty;
        return false;
    }

    private static bool TryResolveWatchSystemLabel(string raw, out string labelId) {
        if (raw.Equals("INBOX", StringComparison.OrdinalIgnoreCase)) {
            labelId = "INBOX";
            return true;
        }
        if (raw.Equals("SENT", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("SENTITEMS", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("SENT ITEMS", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("SENT MAIL", StringComparison.OrdinalIgnoreCase)) {
            labelId = "SENT";
            return true;
        }
        if (raw.Equals("TRASH", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("DELETED", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("DELETED ITEMS", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("DELETEDITEMS", StringComparison.OrdinalIgnoreCase)) {
            labelId = "TRASH";
            return true;
        }
        if (raw.Equals("SPAM", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("JUNK", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("JUNK EMAIL", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("JUNKEMAIL", StringComparison.OrdinalIgnoreCase)) {
            labelId = "SPAM";
            return true;
        }
        if (raw.Equals("DRAFT", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("DRAFTS", StringComparison.OrdinalIgnoreCase)) {
            labelId = "DRAFT";
            return true;
        }

        labelId = string.Empty;
        return false;
    }
}
