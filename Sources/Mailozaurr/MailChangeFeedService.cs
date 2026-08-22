using System.Net;

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
        using var client = await _imapSessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        await using var listener = new ImapIdleListener(client, folder);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new MailChangeFeedResult {
            ProfileId = profile.Id,
            Provider = profile.Kind,
            FolderId = folder,
            CursorKind = "ephemeral-idle",
            SupportsDeletes = false
        };
        listener.MessageArrived += (_, message) => {
            if (result.Changes.Count >= max) return;
            result.Changes.Add(new MailChangeItem {
                MessageId = message.Uid.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Kind = MailChangeKind.Upsert,
                Subject = message.Message.Subject
            });
            if (result.Changes.Count >= max) completion.TrySetResult(true);
        };
        listener.IdleError += (_, exception) => completion.TrySetException(exception);

        await listener.StartAsync(cancellationToken).ConfigureAwait(false);
        using var timeout = new CancellationTokenSource(request.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var delay = Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, linked.Token);
        try {
            var completed = await Task.WhenAny(completion.Task, delay).ConfigureAwait(false);
            if (completed == completion.Task) await completion.Task.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        } finally {
            await listener.StopAsync().ConfigureAwait(false);
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
            using var session = await _graphSessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
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
            using var session = await _gmailSessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
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
        var cursor = NormalizeGraphCursor(request.Cursor, nameof(request));
        using var session = await _graphSessionFactory.ConnectAsync(
            WithMailbox(profile, request.MailboxId), cancellationToken).ConfigureAwait(false);
        var folder = GraphMailReadHandler.ResolveFolder(request.FolderId, profile);
        try {
            var delta = await new GraphMailboxBrowser(session.Client).DeltaMessagesAsync(
                folder, cursor, max, cancellationToken, session.UserId).ConfigureAwait(false);
            var changes = delta.Upserts.Select(message => new MailChangeItem {
                MessageId = message.NativeId,
                Kind = MailChangeKind.Upsert,
                ThreadId = message.NativeThreadId,
                Subject = message.Subject
            }).Concat(delta.DeletedNativeIds.Select(id => new MailChangeItem {
                MessageId = id,
                Kind = MailChangeKind.Delete
            })).Take(max).ToList();
            return new MailChangeFeedResult {
                ProfileId = profile.Id,
                Provider = profile.Kind,
                FolderId = delta.FolderSelector,
                NextCursor = NormalizeGraphCursor(delta.Cursor, "Graph response"),
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
        var cursor = NormalizeGmailCursor(request.Cursor!, nameof(request));
        using var session = await _gmailSessionFactory.ConnectAsync(
            WithMailbox(profile, request.MailboxId), cancellationToken).ConfigureAwait(false);
        var folder = GmailMailReadHandler.ResolveFolder(request.FolderId, profile);
        try {
            var history = await session.Browser.GetHistoryAsync(
                folder, cursor, max, cancellationToken).ConfigureAwait(false);
            var changes = history.UpsertNativeIds.Select(id => new MailChangeItem {
                MessageId = id,
                Kind = MailChangeKind.Upsert
            }).Concat(history.DeletedNativeIds.Select(id => new MailChangeItem {
                MessageId = id,
                Kind = MailChangeKind.Delete
            })).Take(max).ToList();
            return new MailChangeFeedResult {
                ProfileId = profile.Id,
                Provider = profile.Kind,
                FolderId = history.ResolvedLabelId,
                NextCursor = NormalizeGmailCursor(history.NewHistoryId ?? cursor, "Gmail response"),
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
            var folder = request.FolderIds.FirstOrDefault() ?? "INBOX";
            subscription = await browser.CreateMessageSubscriptionAsync(
                request.NotificationUrl!,
                folder: folder,
                expirationDateTime: request.Expiration.Value,
                clientState: request.ClientState,
                cancellationToken: cancellationToken,
                userId: session.UserId).ConfigureAwait(false);
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

    private static string? NormalizeGraphCursor(string? cursor, string parameterName) {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        if (!Uri.TryCreate(cursor!.Trim(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "graph.microsoft.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !uri.AbsolutePath.StartsWith("/v1.0/", StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("A Graph cursor must be an HTTPS Microsoft Graph v1.0 delta URL.", parameterName);
        }
        return uri.AbsoluteUri;
    }

    private static string NormalizeGmailCursor(string cursor, string parameterName) {
        var normalized = cursor.Trim();
        if (normalized.Length == 0 || normalized.Length > 20 || normalized.Any(character => character < '0' || character > '9')) {
            throw new ArgumentException("A Gmail history cursor must be an unsigned decimal history id.", parameterName);
        }
        return normalized;
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
            Settings = new Dictionary<string, string>(profile.Settings, StringComparer.OrdinalIgnoreCase),
            Capabilities = profile.Capabilities
        };
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
