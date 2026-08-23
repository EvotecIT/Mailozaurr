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
    private readonly SemaphoreSlim _methodRequestGate = new(1, 1);
    private JmapSessionResource? _session;
    private int _callSequence;
    private bool _disposed;

    /// <summary>Creates a JMAP client using an optional caller-owned HTTP client.</summary>
    public JmapApiClient(
        Uri sessionUrl,
        string accessToken,
        HttpClient? httpClient = null,
        bool allowCrossOriginApiUrl = false) {
        SessionUrl = ValidateSessionUrl(sessionUrl ?? throw new ArgumentNullException(nameof(sessionUrl)));
        if (string.IsNullOrWhiteSpace(accessToken)) throw new ArgumentException("Access token is required.", nameof(accessToken));
        _accessToken = accessToken.Trim();
        _client = httpClient ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
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
            using var response = await SendSessionDiscoveryAsync(cancellationToken).ConfigureAwait(false);
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
        var maximum = ResolveMaxObjectsInGet(context.Session);
        var mailboxes = new List<JmapMailbox>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var position = 0;
        while (true) {
            var query = await CallAsync(
                context.Session,
                "Mailbox/query",
                new JmapMailboxQueryArguments {
                    AccountId = context.AccountId,
                    Position = position,
                    Limit = maximum
                },
                JmapJsonContext.Default.JmapMailboxQueryArguments,
                JmapJsonContext.Default.JmapMailboxQueryResult,
                cancellationToken).ConfigureAwait(false);
            if (query.Ids.Count == 0) {
                if (query.Total.HasValue && position < query.Total.Value) {
                    throw new JmapApiException("invalidResponse", "JMAP Mailbox/query ended before its reported total was reached.");
                }
                break;
            }
            if (query.Position != position) {
                throw new JmapApiException("invalidResponse", "JMAP Mailbox/query returned an unexpected page position.");
            }
            if (query.Ids.Any(id => !seenIds.Add(id))) {
                throw new JmapApiException("invalidResponse", "JMAP Mailbox/query repeated an identifier across pages.");
            }
            var response = await CallAsync(
                context.Session,
                "Mailbox/get",
                new JmapMailboxGetArguments {
                    AccountId = context.AccountId,
                    Ids = NormalizeIds(query.Ids, nameof(query.Ids), maximum)
                },
                JmapJsonContext.Default.JmapMailboxGetArguments,
                JmapJsonContext.Default.JmapMailboxGetResponse,
                cancellationToken).ConfigureAwait(false);
            if (response.NotFound.Count > 0 || response.List.Count != query.Ids.Count) {
                throw new JmapApiException("notFound", "JMAP did not return every mailbox selected by Mailbox/query.");
            }
            mailboxes.AddRange(response.List);
            position = checked(position + query.Ids.Count);
            if (mailboxes.Count > 5000) {
                throw new JmapApiException("responseTooLarge", "JMAP mailbox listing exceeded the 5000-object safety limit.");
            }
            if (query.Total.HasValue ? position >= query.Total.Value : query.Ids.Count < maximum) break;
        }
        return mailboxes;
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
                Limit = Clamp(limit, 0, 5000),
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
                SinceState = sinceState,
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
        if (response.NotFound.Count > 0) {
            throw new JmapApiException("notFound", $"JMAP did not find {response.NotFound.Count} requested thread identifier(s).");
        }
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
        _methodRequestGate.Dispose();
        if (_ownsClient) _client.Dispose();
    }

    private async Task<HttpResponseMessage> SendSessionDiscoveryAsync(CancellationToken cancellationToken) {
        var current = SessionUrl;
        for (var redirectCount = 0; redirectCount <= 5; redirectCount++) {
            using var request = CreateRequest(HttpMethod.Get, current);
            var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!IsRedirect(response.StatusCode)) return response;
            var location = response.Headers.Location;
            response.Dispose();
            if (location == null) {
                throw new JmapApiException("invalidRedirect", "JMAP session discovery returned a redirect without a Location header.");
            }
            var redirected = location.IsAbsoluteUri ? location : new Uri(current, location);
            redirected = ValidateHttpsUri(redirected, "session redirect URL");
            if (!_allowCrossOriginApiUrl && !HasSameOrigin(SessionUrl, redirected)) {
                throw new JmapApiException("crossOriginRedirect", "JMAP session discovery redirected to a different origin; explicit cross-origin authorization is required.");
            }
            current = redirected;
        }
        throw new JmapApiException("tooManyRedirects", "JMAP session discovery exceeded five redirects.");
    }

    private static bool IsRedirect(System.Net.HttpStatusCode statusCode) =>
        statusCode == System.Net.HttpStatusCode.MovedPermanently ||
        statusCode == System.Net.HttpStatusCode.Redirect ||
        statusCode == System.Net.HttpStatusCode.RedirectMethod ||
        statusCode == System.Net.HttpStatusCode.TemporaryRedirect ||
        (int)statusCode == 308;
}
