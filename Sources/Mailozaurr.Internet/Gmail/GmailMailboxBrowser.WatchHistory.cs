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
    /// <summary>
    /// Gets mailbox profile.
    /// </summary>
    public async Task<GmailMailboxProfileResult> GetProfileAsync(CancellationToken cancellationToken = default) {
        var profile = await _gmail.GetProfileAsync(_userId, cancellationToken).ConfigureAwait(false);
        return MapProfile(profile);
    }

    /// <summary>
    /// Gets mailbox profile without refreshing authentication after a 401/403 response.
    /// </summary>
    public async Task<GmailMailboxProfileResult> GetProfileWithoutRefreshAsync(CancellationToken cancellationToken = default) {
        var profile = await _gmail.GetProfileWithoutRefreshAsync(_userId, cancellationToken).ConfigureAwait(false);
        return MapProfile(profile);
    }

    private static GmailMailboxProfileResult MapProfile(GmailApiClient.GmailProfile profile) =>
        new() {
            EmailAddress = NormalizeOptional(profile.EmailAddress),
            MessagesTotal = profile.MessagesTotal,
            ThreadsTotal = profile.ThreadsTotal,
            HistoryId = NormalizeOptional(profile.HistoryId)
        };

    /// <summary>
    /// Starts Gmail watch subscription for selected folders.
    /// </summary>
    public async Task<GmailMailboxWatchResult> WatchAsync(
        string topicName,
        IReadOnlyCollection<string>? folders,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(topicName)) {
            throw new ArgumentException("topicName is required.", nameof(topicName));
        }

        var folderInput = folders == null || folders.Count == 0
            ? new[] { "INBOX" }
            : folders;

        var labelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in folderInput) {
            if (string.IsNullOrWhiteSpace(raw)) {
                continue;
            }
            var labelId = await ResolveWatchLabelIdAsync(raw, cancellationToken).ConfigureAwait(false);
            var normalizedLabelId = NormalizeOptional(labelId);
            if (normalizedLabelId != null) {
                labelIds.Add(normalizedLabelId);
            }
        }

        var watch = await _gmail.WatchAsync(
            _userId,
            topicName.Trim(),
            labelIds.Count == 0 ? null : labelIds.ToArray(),
            cancellationToken).ConfigureAwait(false);

        DateTime? expirationUtc = null;
        if (watch.Expiration > 0) {
            expirationUtc = DateTimeOffset.FromUnixTimeMilliseconds(watch.Expiration).UtcDateTime;
        }

        return new GmailMailboxWatchResult {
            HistoryId = NormalizeOptional(watch.HistoryId),
            ExpirationUtc = expirationUtc,
            LabelIds = labelIds.ToList()
        };
    }

    /// <summary>
    /// Stops Gmail watch subscription.
    /// </summary>
    public Task StopWatchAsync(CancellationToken cancellationToken = default) =>
        _gmail.StopWatchAsync(_userId, cancellationToken);

    /// <summary>
    /// Stops Gmail watch subscription with stale-remote handling.
    /// </summary>
    public async Task<GmailMailboxStopWatchResult> StopWatchAsync(
        bool treatMissingAsSuccess,
        CancellationToken cancellationToken = default) {
        try {
            await _gmail.StopWatchAsync(_userId, cancellationToken).ConfigureAwait(false);
            return new GmailMailboxStopWatchResult {
                Stopped = true
            };
        } catch (GmailApiException ex) when (treatMissingAsSuccess &&
                                             (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Gone)) {
            return new GmailMailboxStopWatchResult {
                Stopped = true,
                AlreadyStopped = true
            };
        }
    }

    /// <summary>
    /// Gets Gmail history changes for a folder.
    /// </summary>
    public async Task<GmailMailboxHistoryResult> GetHistoryAsync(
        string folder,
        string startHistoryId,
        int maxChanges,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(startHistoryId)) {
            throw new ArgumentException("startHistoryId is required.", nameof(startHistoryId));
        }

        var finalStates = new Dictionary<string, bool>(StringComparer.Ordinal);
        string? pageToken = null;
        string? newHistoryId = null;
        string? resolvedLabelId = null;
        var seenPageTokens = new HashSet<string>(StringComparer.Ordinal);
        const int maxProviderPages = 25;
        var pagesRead = 0;

        do {
            var page = await GetHistoryPageAsync(
                folder,
                startHistoryId,
                maxChanges,
                pageToken,
                cancellationToken).ConfigureAwait(false);
            resolvedLabelId = page.ResolvedLabelId;
            newHistoryId = page.NewHistoryId ?? newHistoryId;
            foreach (var id in page.DeletedNativeIds) finalStates[id] = true;
            foreach (var id in page.UpsertNativeIds) finalStates[id] = false;

            pageToken = NormalizeOptional(page.NextPageToken);
            if (pageToken != null && !seenPageTokens.Add(pageToken)) {
                throw new InvalidDataException("Gmail history pagination returned a repeated page token.");
            }
            pagesRead++;

            // A Gmail page is the smallest safe continuation boundary: never
            // discard events from the page that crossed the requested limit.
            // Empty pages may be followed by changes, but the legacy helper is
            // still bounded so a hostile or enormous backlog cannot drain
            // indefinitely. Callers can resume from NextPageToken.
            if (finalStates.Count >= ClampInt(maxChanges, 1, 500) || pagesRead >= maxProviderPages) {
                break;
            }
        } while (pageToken != null);

        var upsertIds = finalStates.Where(pair => !pair.Value).Select(pair => pair.Key).ToList();
        upsertIds.Sort(StringComparer.Ordinal);
        var deleteIds = finalStates.Where(pair => pair.Value).Select(pair => pair.Key).ToList();
        deleteIds.Sort(StringComparer.Ordinal);

        return new GmailMailboxHistoryResult {
            ResolvedLabelId = resolvedLabelId ?? string.Empty,
            NewHistoryId = newHistoryId,
            NextPageToken = pageToken,
            UpsertNativeIds = upsertIds,
            DeletedNativeIds = deleteIds
        };
    }

    /// <summary>
    /// Gets exactly one Gmail history provider page without discarding events from that page.
    /// </summary>
    public async Task<GmailMailboxHistoryResult> GetHistoryPageAsync(
        string folder,
        string startHistoryId,
        int pageSize,
        string? pageToken = null,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(startHistoryId)) {
            throw new ArgumentException("startHistoryId is required.", nameof(startHistoryId));
        }

        var resolvedLabelId = NormalizeOptional(await ResolveLabelIdAsync(folder, cancellationToken).ConfigureAwait(false));
        if (resolvedLabelId == null) {
            throw new InvalidOperationException("Unable to resolve Gmail folder/label.");
        }

        var history = await _gmail.ListHistoryAsync(
            _userId,
            startHistoryId.Trim(),
            labelId: resolvedLabelId,
            historyTypes: new[] { "messageAdded", "messageDeleted", "labelAdded", "labelRemoved" },
            maxResults: ClampInt(pageSize, 1, 500),
            pageToken: NormalizeOptional(pageToken),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var finalStates = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (history.History != null) {
            foreach (var entry in history.History) {
                if (entry == null) continue;
                var entryUpserts = new HashSet<string>(StringComparer.Ordinal);
                AddHistoryRefs(entry.MessagesAdded, entryUpserts);
                AddHistoryRefs(entry.LabelsAdded, entryUpserts);
                foreach (var id in entryUpserts) finalStates[id] = false;

                var entryDeletes = new HashSet<string>(StringComparer.Ordinal);
                AddHistoryRefs(entry.MessagesDeleted, entryDeletes);
                AddHistoryRefs(entry.LabelsRemoved, entryDeletes);
                foreach (var id in entryDeletes) finalStates[id] = true;
            }
        }

        var upsertIds = finalStates.Where(pair => !pair.Value).Select(pair => pair.Key).ToList();
        upsertIds.Sort(StringComparer.Ordinal);
        var deleteIds = finalStates.Where(pair => pair.Value).Select(pair => pair.Key).ToList();
        deleteIds.Sort(StringComparer.Ordinal);
        return new GmailMailboxHistoryResult {
            ResolvedLabelId = resolvedLabelId,
            NewHistoryId = NormalizeOptional(history.HistoryId),
            NextPageToken = NormalizeOptional(history.NextPageToken),
            UpsertNativeIds = upsertIds,
            DeletedNativeIds = deleteIds
        };
    }
}
