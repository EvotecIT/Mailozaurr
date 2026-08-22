using System.Net;
using System.Text;

namespace Mailozaurr;

/// <summary>Normalizes IMAP IDLE, Graph delta/subscriptions, and Gmail history/watch.</summary>
public sealed class MailChangeFeedService : IMailChangeFeedService {
    private readonly IMailProfileStore _profileStore;
    private readonly IImapSessionFactory _imapSessionFactory;
    private readonly IGraphSessionFactory _graphSessionFactory;
    private readonly IGmailSessionFactory _gmailSessionFactory;

    /// <summary>Creates the default normalized change-feed service.</summary>
    public MailChangeFeedService(
        IMailProfileStore profileStore,
        IImapSessionFactory imapSessionFactory,
        IGraphSessionFactory graphSessionFactory,
        IGmailSessionFactory gmailSessionFactory) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _imapSessionFactory = imapSessionFactory ?? throw new ArgumentNullException(nameof(imapSessionFactory));
        _graphSessionFactory = graphSessionFactory ?? throw new ArgumentNullException(nameof(graphSessionFactory));
        _gmailSessionFactory = gmailSessionFactory ?? throw new ArgumentNullException(nameof(gmailSessionFactory));
    }

    /// <inheritdoc />
    public async Task<MailChangeFeedResult> GetChangesAsync(
        MailChangeFeedRequest request,
        CancellationToken cancellationToken = default) {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var profile = await GetProfileAsync(request.ProfileId, cancellationToken).ConfigureAwait(false);
        var max = ClampMax(request.MaxChanges);
        switch (profile.Kind) {
            case MailProfileKind.Graph:
                return await GetGraphChangesAsync(profile, request, max, cancellationToken).ConfigureAwait(false);
            case MailProfileKind.Gmail:
                return await GetGmailChangesAsync(profile, request, max, cancellationToken).ConfigureAwait(false);
            case MailProfileKind.Imap:
                throw new NotSupportedException("IMAP IDLE is ephemeral. Use WaitForChangesAsync instead of a durable cursor read.");
            default:
                throw new NotSupportedException($"Change feeds are not supported for profile kind '{profile.Kind}'.");
        }
    }

    /// <inheritdoc />
    public async Task<MailChangeFeedResult> WaitForChangesAsync(
        MailChangeWaitRequest request,
        CancellationToken cancellationToken = default) {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Timeout <= TimeSpan.Zero || request.Timeout > TimeSpan.FromHours(1)) {
            throw new ArgumentOutOfRangeException(nameof(request.Timeout));
        }
        var profile = await GetProfileAsync(request.ProfileId, cancellationToken).ConfigureAwait(false);
        if (profile.Kind != MailProfileKind.Imap) {
            throw new NotSupportedException("Bounded live waiting currently requires an IMAP profile.");
        }

        var max = ClampMax(request.MaxChanges);
        var folder = ImapMailReadHandler.ResolveFolder(request.FolderId, profile);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new MailChangeFeedResult {
            ProfileId = profile.Id,
            Provider = profile.Kind,
            FolderId = folder,
            CursorKind = "ephemeral-idle",
            SupportsDeletes = false
        };
        using var timeout = new CancellationTokenSource(request.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try {
            using var client = await _imapSessionFactory.ConnectAsync(profile, linked.Token).ConfigureAwait(false);
            await using var listener = new ImapIdleListener(
                client,
                folder,
                searchQuery: null,
                downloadMessageContent: false);
            listener.MessageSummaryArrived += (_, message) => {
                result.Changes.Add(new MailChangeItem {
                    MessageId = message.Uid.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Kind = MailChangeKind.Upsert,
                    Subject = message.Subject
                });
                if (result.Changes.Count >= max) completion.TrySetResult(true);
            };
            listener.IdleError += (_, exception) => completion.TrySetException(exception);

            await listener.StartAsync(linked.Token).ConfigureAwait(false);
            var delay = Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, linked.Token);
            var completed = await Task.WhenAny(completion.Task, delay).ConfigureAwait(false);
            if (completed == completion.Task) await completion.Task.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await listener.StopAsync().ConfigureAwait(false);
        } catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            // A bounded observation that times out returns the arrivals collected so far.
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<MailChangeSubscriptionResult> SubscribeAsync(
        MailChangeSubscriptionRequest request,
        CancellationToken cancellationToken = default) {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var profile = await GetProfileAsync(request.ProfileId, cancellationToken).ConfigureAwait(false);
        switch (profile.Kind) {
            case MailProfileKind.Graph:
                return await SubscribeGraphAsync(profile, request, cancellationToken).ConfigureAwait(false);
            case MailProfileKind.Gmail:
                return await SubscribeGmailAsync(profile, request, cancellationToken).ConfigureAwait(false);
            case MailProfileKind.Imap:
                throw new NotSupportedException("IMAP IDLE subscriptions are connection-scoped. Use WaitForChangesAsync.");
            default:
                throw new NotSupportedException($"Subscriptions are not supported for profile kind '{profile.Kind}'.");
        }
    }

    /// <inheritdoc />
    public async Task<MailChangeSubscriptionResult> UnsubscribeAsync(
        MailChangeUnsubscribeRequest request,
        CancellationToken cancellationToken = default) {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var profile = await GetProfileAsync(request.ProfileId, cancellationToken).ConfigureAwait(false);
        if (profile.Kind == MailProfileKind.Graph) {
            if (string.IsNullOrWhiteSpace(request.SubscriptionId)) {
                throw new ArgumentException("A Graph subscription id is required.", nameof(request));
            }
            using var session = await _graphSessionFactory.ConnectAsync(
                WithMailbox(profile, request.MailboxId), cancellationToken).ConfigureAwait(false);
            var deleted = await new GraphMailboxBrowser(session.Client).DeleteSubscriptionAsync(
                request.SubscriptionId!, request.TreatMissingAsSuccess, cancellationToken).ConfigureAwait(false);
            return new MailChangeSubscriptionResult {
                ProfileId = profile.Id,
                Provider = profile.Kind,
                Succeeded = deleted.Deleted,
                AlreadyMissing = deleted.AlreadyDeleted,
                SubscriptionId = request.SubscriptionId!.Trim()
            };
        }
        if (profile.Kind == MailProfileKind.Gmail) {
            using var session = await _gmailSessionFactory.ConnectAsync(
                WithMailbox(profile, request.MailboxId), cancellationToken).ConfigureAwait(false);
            var stopped = await session.Browser.StopWatchAsync(
                request.TreatMissingAsSuccess, cancellationToken).ConfigureAwait(false);
            return new MailChangeSubscriptionResult {
                ProfileId = profile.Id,
                Provider = profile.Kind,
                Succeeded = stopped.Stopped,
                AlreadyMissing = stopped.AlreadyStopped
            };
        }
        throw new NotSupportedException($"Subscriptions are not supported for profile kind '{profile.Kind}'.");
    }

    private async Task<MailChangeFeedResult> GetGraphChangesAsync(
        MailProfile profile,
        MailChangeFeedRequest request,
        int max,
        CancellationToken cancellationToken) {
        var effectiveProfile = WithMailbox(profile, request.MailboxId);
        var folder = GraphMailReadHandler.ResolveFolder(request.FolderId, profile);
        var folderSelector = GraphMailboxBrowser.ResolveFolderSelector(folder);
        var expectedUserId = GraphMailReadHandler.ResolveUserId(effectiveProfile);
        var cursor = NormalizeGraphCursor(request.Cursor, expectedUserId, folderSelector, nameof(request));
        using var session = await _graphSessionFactory.ConnectAsync(
            effectiveProfile, cancellationToken).ConfigureAwait(false);
        if (cursor != null) {
            cursor = NormalizeGraphCursor(cursor, session.UserId, folderSelector, nameof(request));
        }
        try {
            var delta = await new GraphMailboxBrowser(session.Client).DeltaMessagesForUserAsync(
                folder, cursor, max, session.UserId, cancellationToken).ConfigureAwait(false);
            var changes = delta.Upserts.Select(message => new MailChangeItem {
                MessageId = message.NativeId,
                Kind = MailChangeKind.Upsert,
                ThreadId = message.NativeThreadId,
                Subject = message.Subject
            }).Concat(delta.DeletedNativeIds.Select(id => new MailChangeItem {
                MessageId = id,
                Kind = MailChangeKind.Delete
            })).ToList();
            var nextCursor = NormalizeGraphResponseCursor(delta.Cursor, session.UserId, folderSelector);
            return new MailChangeFeedResult {
                ProfileId = profile.Id,
                Provider = profile.Kind,
                FolderId = delta.FolderSelector,
                NextCursor = nextCursor,
                CursorKind = "durable-delta",
                SupportsDeletes = true,
                Changes = changes
            };
        } catch (GraphApiException ex) when (ex.StatusCode == HttpStatusCode.Gone) {
            return ResetRequired(profile, folder, "durable-delta");
        }
    }

    private async Task<MailChangeFeedResult> GetGmailChangesAsync(
        MailProfile profile,
        MailChangeFeedRequest request,
        int max,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(request.Cursor)) {
            throw new ArgumentException("A Gmail history cursor is required.", nameof(request));
        }
        var cursor = ParseGmailCursor(request.Cursor!, nameof(request));
        using var session = await _gmailSessionFactory.ConnectAsync(
            WithMailbox(profile, request.MailboxId), cancellationToken).ConfigureAwait(false);
        var folder = GmailMailReadHandler.ResolveFolder(request.FolderId, profile);
        try {
            var history = await session.Browser.GetHistoryPageAsync(
                folder, cursor.StartHistoryId, max, cursor.PageToken, cancellationToken).ConfigureAwait(false);
            var changes = history.UpsertNativeIds.Select(id => new MailChangeItem {
                MessageId = id,
                Kind = MailChangeKind.Upsert
            }).Concat(history.DeletedNativeIds.Select(id => new MailChangeItem {
                MessageId = id,
                Kind = MailChangeKind.Delete
            })).ToList();
            var responseHistoryId = NormalizeGmailResponseHistoryId(history.NewHistoryId);
            var nextCursor = string.IsNullOrWhiteSpace(history.NextPageToken)
                ? responseHistoryId
                : EncodeGmailCursor(cursor.StartHistoryId, history.NextPageToken!);
            return new MailChangeFeedResult {
                ProfileId = profile.Id,
                Provider = profile.Kind,
                FolderId = history.ResolvedLabelId,
                NextCursor = nextCursor,
                CursorKind = "durable-history",
                SupportsDeletes = true,
                Changes = changes
            };
        } catch (GmailApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
            return ResetRequired(profile, folder, "durable-history");
        }
    }

    private async Task<MailChangeSubscriptionResult> SubscribeGraphAsync(
        MailProfile profile,
        MailChangeSubscriptionRequest request,
        CancellationToken cancellationToken) {
        using var session = await _graphSessionFactory.ConnectAsync(
            WithMailbox(profile, request.MailboxId), cancellationToken).ConfigureAwait(false);
        var browser = new GraphMailboxBrowser(session.Client);
        GraphMailboxBrowser.GraphMailboxSubscriptionResult subscription;
        if (!string.IsNullOrWhiteSpace(request.SubscriptionId)) {
            if (!request.Expiration.HasValue) {
                throw new ArgumentException("Graph renewal requires an expiration value.", nameof(request));
            }
            subscription = await browser.RenewSubscriptionAsync(
                request.SubscriptionId!, request.Expiration.Value, cancellationToken).ConfigureAwait(false);
        } else {
            if (string.IsNullOrWhiteSpace(request.NotificationUrl) || !request.Expiration.HasValue) {
                throw new ArgumentException("Graph subscription creation requires notification URL and expiration.", nameof(request));
            }
            var folders = request.FolderIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (folders.Count > 1) {
                throw new ArgumentException("A Graph subscription supports exactly one folder resource.", nameof(request));
            }
            var folder = folders.FirstOrDefault() ?? "INBOX";
            subscription = await browser.CreateMessageSubscriptionForUserAsync(
                request.NotificationUrl!,
                folder,
                request.Expiration.Value,
                "created,updated,deleted",
                request.ClientState,
                session.UserId,
                cancellationToken).ConfigureAwait(false);
        }
        return new MailChangeSubscriptionResult {
            ProfileId = profile.Id,
            Provider = profile.Kind,
            Succeeded = true,
            SubscriptionId = subscription.SubscriptionId,
            Resource = subscription.Resource,
            Expiration = subscription.ExpirationDateTime
        };
    }

    private async Task<MailChangeSubscriptionResult> SubscribeGmailAsync(
        MailProfile profile,
        MailChangeSubscriptionRequest request,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(request.TopicName)) {
            throw new ArgumentException("Gmail watch requires a Pub/Sub topic name.", nameof(request));
        }
        using var session = await _gmailSessionFactory.ConnectAsync(
            WithMailbox(profile, request.MailboxId), cancellationToken).ConfigureAwait(false);
        var watch = await session.Browser.WatchAsync(
            request.TopicName!, request.FolderIds, cancellationToken).ConfigureAwait(false);
        return new MailChangeSubscriptionResult {
            ProfileId = profile.Id,
            Provider = profile.Kind,
            Succeeded = true,
            Cursor = watch.HistoryId,
            Expiration = watch.ExpirationUtc.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(watch.ExpirationUtc.Value, DateTimeKind.Utc))
                : null,
            FolderIds = watch.LabelIds
        };
    }

    private async Task<MailProfile> GetProfileAsync(string profileId, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("Profile id is required.", nameof(profileId));
        return await _profileStore.GetByIdAsync(profileId.Trim(), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");
    }

    private static int ClampMax(int max) {
        if (max < 1) throw new ArgumentOutOfRangeException(nameof(max));
        return Math.Min(max, 2000);
    }

    private static string? NormalizeGraphCursor(
        string? cursor,
        string userId,
        string folderSelector,
        string parameterName) {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        if (!Uri.TryCreate(cursor!.Trim(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "graph.microsoft.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !IsExpectedGraphDeltaPath(uri, userId, folderSelector)) {
            throw new ArgumentException(
                "A Graph cursor must be an HTTPS Microsoft Graph v1.0 delta URL for the requested mailbox and folder.",
                parameterName);
        }
        return uri.AbsoluteUri;
    }

    private static bool IsExpectedGraphDeltaPath(Uri uri, string userId, string folderSelector) {
        string[] segments;
        try {
            segments = uri.AbsolutePath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.UnescapeDataString)
                .ToArray();
        } catch (UriFormatException) {
            return false;
        }

        var normalizedUser = string.IsNullOrWhiteSpace(userId) ? "me" : userId.Trim();
        if (string.Equals(normalizedUser, "me", StringComparison.OrdinalIgnoreCase)) {
            return segments.Length == 6 &&
                   string.Equals(segments[0], "v1.0", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(segments[1], "me", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(segments[2], "mailFolders", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(segments[3], folderSelector, StringComparison.Ordinal) &&
                   string.Equals(segments[4], "messages", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(segments[5], "delta", StringComparison.OrdinalIgnoreCase);
        }

        return segments.Length == 7 &&
               string.Equals(segments[0], "v1.0", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[1], "users", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[2], normalizedUser, StringComparison.Ordinal) &&
               string.Equals(segments[3], "mailFolders", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[4], folderSelector, StringComparison.Ordinal) &&
               string.Equals(segments[5], "messages", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[6], "delta", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeGmailCursor(string cursor, string parameterName) {
        var normalized = cursor.Trim();
        if (normalized.Length == 0 || normalized.Length > 20 || normalized.Any(character => character < '0' || character > '9')) {
            throw new ArgumentException("A Gmail history cursor must be an unsigned decimal history id.", parameterName);
        }
        return normalized;
    }

    private static GmailCursorState ParseGmailCursor(string cursor, string parameterName) {
        var normalized = cursor.Trim();
        if (!normalized.StartsWith("gmail-v1.", StringComparison.Ordinal)) {
            return new GmailCursorState(NormalizeGmailCursor(normalized, parameterName), null);
        }
        if (normalized.Length > 8192) {
            throw new ArgumentException("The Gmail history cursor is too long.", parameterName);
        }
        var payload = normalized.Substring("gmail-v1.".Length);
        var separator = payload.IndexOf('.');
        if (separator <= 0 || separator == payload.Length - 1 || payload.IndexOf('.', separator + 1) >= 0) {
            throw new ArgumentException("The Gmail history cursor is invalid.", parameterName);
        }
        var startHistoryId = NormalizeGmailCursor(payload.Substring(0, separator), parameterName);
        string pageToken;
        try {
            pageToken = DecodeBase64Url(payload.Substring(separator + 1));
        } catch (FormatException ex) {
            throw new ArgumentException("The Gmail history cursor is invalid.", parameterName, ex);
        }
        if (string.IsNullOrWhiteSpace(pageToken) || pageToken.Length > 4096) {
            throw new ArgumentException("The Gmail history cursor contains an invalid page token.", parameterName);
        }
        return new GmailCursorState(startHistoryId, pageToken);
    }

    private static string EncodeGmailCursor(string startHistoryId, string pageToken) {
        if (string.IsNullOrWhiteSpace(pageToken) || pageToken.Length > 4096) {
            throw new InvalidDataException("Gmail history response contained an invalid page token.");
        }
        return "gmail-v1." + NormalizeGmailCursor(startHistoryId, nameof(startHistoryId)) + "." + EncodeBase64Url(pageToken);
    }

    private static string EncodeBase64Url(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static string DecodeBase64Url(string value) {
        var encoded = value.Replace('-', '+').Replace('_', '/');
        switch (encoded.Length % 4) {
            case 2: encoded += "=="; break;
            case 3: encoded += "="; break;
            case 1: throw new FormatException("Invalid base64url length.");
        }
        return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
    }

    private static string NormalizeGraphResponseCursor(string? cursor, string userId, string folderSelector) {
        try {
            return NormalizeGraphCursor(cursor, userId, folderSelector, "Graph response")
                   ?? throw new InvalidDataException("Graph delta response did not contain a continuation cursor.");
        } catch (ArgumentException ex) {
            throw new InvalidDataException("Graph delta response contained an invalid continuation cursor.", ex);
        }
    }

    private static string NormalizeGmailResponseHistoryId(string? historyId) {
        if (string.IsNullOrWhiteSpace(historyId)) {
            throw new InvalidDataException("Gmail history response did not contain a history id.");
        }
        try {
            return NormalizeGmailCursor(historyId!, "Gmail response");
        } catch (ArgumentException ex) {
            throw new InvalidDataException("Gmail history response contained an invalid history id.", ex);
        }
    }

    private static MailProfile WithMailbox(MailProfile profile, string? mailboxId) {
        if (string.IsNullOrWhiteSpace(mailboxId)) return profile;
        return new MailProfile {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            Description = profile.Description,
            Kind = profile.Kind,
            DefaultSender = profile.DefaultSender,
            DefaultMailbox = mailboxId!.Trim(),
            IsDefault = profile.IsDefault,
            Settings = new Dictionary<string, string>(profile.Settings, StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = mailboxId.Trim()
            },
            Capabilities = profile.Capabilities
        };
    }

    private sealed class GmailCursorState {
        internal GmailCursorState(string startHistoryId, string? pageToken) {
            StartHistoryId = startHistoryId;
            PageToken = pageToken;
        }

        internal string StartHistoryId { get; }
        internal string? PageToken { get; }
    }

    private static MailChangeFeedResult ResetRequired(MailProfile profile, string folder, string cursorKind) => new() {
        ProfileId = profile.Id,
        Provider = profile.Kind,
        FolderId = folder,
        CursorKind = cursorKind,
        ResetRequired = true,
        SupportsDeletes = true
    };
}
