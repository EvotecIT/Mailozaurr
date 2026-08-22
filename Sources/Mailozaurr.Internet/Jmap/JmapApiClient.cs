using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mailozaurr;

/// <summary>Bounded RFC 8620 and RFC 8621 client for discovery and core mail reads.</summary>
public sealed partial class JmapApiClient : IDisposable {
    private const int DefaultMaximumResponseBytes = 16 * 1024 * 1024;
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly string _accessToken;
    private readonly bool _allowCrossOriginApiUrl;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private JmapSessionResource? _session;
    private int _callSequence;
    private bool _disposed;

    /// <summary>Creates a JMAP client using an optional caller-owned HTTP client.</summary>
    public JmapApiClient(
        Uri sessionUrl,
        string accessToken,
        HttpClient? httpClient = null,
        bool allowCrossOriginApiUrl = false) {
        SessionUrl = ValidateHttpsUri(sessionUrl ?? throw new ArgumentNullException(nameof(sessionUrl)), "session URL");
        if (string.IsNullOrWhiteSpace(accessToken)) throw new ArgumentException("Access token is required.", nameof(accessToken));
        _accessToken = accessToken.Trim();
        _client = httpClient ?? new HttpClient();
        _ownsClient = httpClient == null;
        _allowCrossOriginApiUrl = allowCrossOriginApiUrl;
    }

    /// <summary>Configured JMAP Session resource URL.</summary>
    public Uri SessionUrl { get; }

    /// <summary>Maximum accepted bytes for one JMAP response.</summary>
    public int MaximumResponseBytes { get; set; } = DefaultMaximumResponseBytes;

    /// <summary>Discovers and validates the JMAP Session resource.</summary>
    public async Task<JmapSessionResource> GetSessionAsync(bool forceRefresh = false, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (!forceRefresh && _session != null) return _session;
        await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (!forceRefresh && _session != null) return _session;
            using var request = CreateRequest(HttpMethod.Get, SessionUrl);
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
                throw new JmapApiException("sessionRequestFailed", $"JMAP session discovery failed ({(int)response.StatusCode}).");
            }
            var payload = await ReadBoundedAsync(response.Content, cancellationToken).ConfigureAwait(false);
            JmapSessionResource? session;
            try {
                session = JsonSerializer.Deserialize(payload, JmapJsonContext.Default.JmapSessionResource);
            } catch (JsonException ex) {
                throw new JmapApiException("invalidSession", "JMAP returned an invalid Session resource.") { Source = ex.Source };
            }
            ValidateSession(session);
            _session = session;
            return session!;
        } finally {
            _sessionLock.Release();
        }
    }

    /// <summary>Lists mailboxes for a mail-capable account.</summary>
    public async Task<IReadOnlyList<JmapMailbox>> ListMailboxesAsync(string? accountId = null, CancellationToken cancellationToken = default) {
        var context = await ResolveAccountAsync(accountId, JmapCapabilities.Mail, cancellationToken).ConfigureAwait(false);
        var response = await CallAsync(
            context.Session,
            "Mailbox/get",
            new JmapMailboxGetArguments { AccountId = context.AccountId },
            JmapJsonContext.Default.JmapMailboxGetArguments,
            JmapJsonContext.Default.JmapMailboxGetResponse,
            cancellationToken).ConfigureAwait(false);
        return response.List;
    }

    /// <summary>Queries email identifiers using a bounded RFC 8621 filter and sort.</summary>
    public async Task<JmapEmailQueryResult> QueryEmailsAsync(
        JmapEmailFilter? filter = null,
        IReadOnlyList<JmapComparator>? sort = null,
        int position = 0,
        int limit = 100,
        bool collapseThreads = false,
        string? accountId = null,
        CancellationToken cancellationToken = default) {
        var context = await ResolveAccountAsync(accountId, JmapCapabilities.Mail, cancellationToken).ConfigureAwait(false);
        var result = await CallAsync(
            context.Session,
            "Email/query",
            new JmapEmailQueryArguments {
                AccountId = context.AccountId,
                Filter = filter,
                Sort = sort?.ToList(),
                Position = position,
                Limit = Clamp(limit, 1, 5000),
                CollapseThreads = collapseThreads
            },
            JmapJsonContext.Default.JmapEmailQueryArguments,
            JmapJsonContext.Default.JmapEmailQueryResult,
            cancellationToken).ConfigureAwait(false);
        result.HasMore = result.Total.HasValue && result.Position + result.Ids.Count < result.Total.Value;
        return result;
    }

    /// <summary>Gets a bounded set of email objects.</summary>
    public async Task<JmapEmailGetResult> GetEmailsAsync(
        IReadOnlyCollection<string> ids,
        IReadOnlyCollection<string>? properties = null,
        string? accountId = null,
        CancellationToken cancellationToken = default) {
        var context = await ResolveAccountAsync(accountId, JmapCapabilities.Mail, cancellationToken).ConfigureAwait(false);
        return await CallAsync(
            context.Session,
            "Email/get",
            new JmapEmailGetArguments {
                AccountId = context.AccountId,
                Ids = NormalizeIds(ids, nameof(ids), ResolveMaxObjectsInGet(context.Session)),
                Properties = NormalizeProperties(properties)
            },
            JmapJsonContext.Default.JmapEmailGetArguments,
            JmapJsonContext.Default.JmapEmailGetResult,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets bounded Email changes since an opaque JMAP state token.</summary>
    public async Task<JmapEmailChangesResult> GetEmailChangesAsync(
        string sinceState,
        int maxChanges = 1000,
        string? accountId = null,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(sinceState)) throw new ArgumentException("Since-state token is required.", nameof(sinceState));
        var context = await ResolveAccountAsync(accountId, JmapCapabilities.Mail, cancellationToken).ConfigureAwait(false);
        return await CallAsync(
            context.Session,
            "Email/changes",
            new JmapEmailChangesArguments {
                AccountId = context.AccountId,
                SinceState = sinceState.Trim(),
                MaxChanges = Clamp(maxChanges, 1, 5000)
            },
            JmapJsonContext.Default.JmapEmailChangesArguments,
            JmapJsonContext.Default.JmapEmailChangesResult,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets a bounded set of thread objects.</summary>
    public async Task<IReadOnlyList<JmapThread>> GetThreadsAsync(
        IReadOnlyCollection<string> ids,
        string? accountId = null,
        CancellationToken cancellationToken = default) {
        var context = await ResolveAccountAsync(accountId, JmapCapabilities.Mail, cancellationToken).ConfigureAwait(false);
        var response = await CallAsync(
            context.Session,
            "Thread/get",
            new JmapThreadGetArguments {
                AccountId = context.AccountId,
                Ids = NormalizeIds(ids, nameof(ids), ResolveMaxObjectsInGet(context.Session))
            },
            JmapJsonContext.Default.JmapThreadGetArguments,
            JmapJsonContext.Default.JmapThreadGetResponse,
            cancellationToken).ConfigureAwait(false);
        return response.List;
    }

    /// <summary>Lists identities when the account advertises JMAP submission.</summary>
    public async Task<IReadOnlyList<JmapIdentity>> ListIdentitiesAsync(string? accountId = null, CancellationToken cancellationToken = default) {
        var context = await ResolveAccountAsync(accountId, JmapCapabilities.Submission, cancellationToken).ConfigureAwait(false);
        var response = await CallAsync(
            context.Session,
            "Identity/get",
            new JmapIdentityGetArguments { AccountId = context.AccountId },
            JmapJsonContext.Default.JmapIdentityGetArguments,
            JmapJsonContext.Default.JmapIdentityGetResponse,
            cancellationToken).ConfigureAwait(false);
        return response.List;
    }

    /// <inheritdoc />
    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        _sessionLock.Dispose();
        if (_ownsClient) _client.Dispose();
    }
}
