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

        var resolvedLabelId = NormalizeOptional(await ResolveLabelIdAsync(folder, cancellationToken).ConfigureAwait(false));
        if (resolvedLabelId == null) {
            throw new InvalidOperationException("Unable to resolve Gmail folder/label.");
        }

        var max = ClampInt(maxChanges, 1, 2000);
        var upserts = new HashSet<string>(StringComparer.Ordinal);
        var deletes = new HashSet<string>(StringComparer.Ordinal);
        var historyTypes = new[] { "messageAdded", "messageDeleted", "labelAdded", "labelRemoved" };
        string? pageToken = null;
        string? newHistoryId = null;
        var pageCount = 0;

        while (pageCount++ < 25 && (upserts.Count + deletes.Count) < max) {
            var history = await _gmail.ListHistoryAsync(
                _userId,
                startHistoryId.Trim(),
                labelId: resolvedLabelId,
                historyTypes: historyTypes,
                maxResults: 500,
                pageToken: pageToken,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var historyId = NormalizeOptional(history.HistoryId);
            if (historyId != null) {
                newHistoryId = historyId;
            }

            pageToken = NormalizeOptional(history.NextPageToken);
            if (history.History == null || history.History.Count == 0) {
                break;
            }

            foreach (var entry in history.History) {
                if (entry == null) {
                    continue;
                }

                AddHistoryRefs(entry.MessagesAdded, upserts);
                AddHistoryRefs(entry.LabelsAdded, upserts);
                AddHistoryRefs(entry.MessagesDeleted, deletes);
                AddHistoryRefs(entry.LabelsRemoved, deletes);
            }

            if (string.IsNullOrWhiteSpace(pageToken)) {
                break;
            }
        }

        foreach (var deletedId in deletes) {
            _ = upserts.Remove(deletedId);
        }

        var upsertIds = upserts.ToList();
        upsertIds.Sort(StringComparer.Ordinal);
        var deleteIds = deletes.ToList();
        deleteIds.Sort(StringComparer.Ordinal);

        return new GmailMailboxHistoryResult {
            ResolvedLabelId = resolvedLabelId,
            NewHistoryId = newHistoryId,
            UpsertNativeIds = upsertIds,
            DeletedNativeIds = deleteIds
        };
    }
}
