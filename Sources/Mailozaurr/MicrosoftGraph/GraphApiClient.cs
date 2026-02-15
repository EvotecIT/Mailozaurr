using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Lightweight client for interacting with Microsoft Graph REST API using a bearer access token.
/// </summary>
/// <remarks>
/// This is a minimal helper focused on reusable primitives needed by apps (for example, managing webhook subscriptions).
/// </remarks>
public sealed class GraphApiClient : IDisposable {
    private readonly HttpClient _client;
    private readonly Func<CancellationToken, Task<string>>? _refreshToken;
    private readonly OAuthCredential? _credential;
    private bool _disposed;

    private void ThrowIfDisposed() {
        if (_disposed) {
            throw new ObjectDisposedException(nameof(GraphApiClient));
        }
    }

    private void ApplyAuthHeader(HttpRequestMessage request) {
        // Avoid mutating HttpClient.DefaultRequestHeaders.Authorization (thread-safety + token refresh semantics).
        // If no credential was provided, we assume the caller configured auth on the HttpClient itself.
        if (_credential != null && !string.IsNullOrWhiteSpace(_credential.AccessToken) && request.Headers.Authorization == null) {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _credential.AccessToken);
        }
    }

    /// <summary>
    /// Initializes the client using the provided OAuth credential.
    /// </summary>
    /// <param name="credential">OAuth credential holding an access token.</param>
    /// <param name="refreshToken">
    /// Optional delegate used to refresh an access token when a request returns 401/403.
    /// Note: the client updates its Authorization header (and the credential's AccessToken, if provided), but does not retry the failed request automatically.
    /// </param>
    /// <param name="baseAddress">Optional Graph base address (defaults to v1.0 endpoint).</param>
    public GraphApiClient(
        OAuthCredential credential,
        Func<CancellationToken, Task<string>>? refreshToken = null,
        Uri? baseAddress = null) {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _refreshToken = refreshToken;
        _client = new HttpClient {
            BaseAddress = baseAddress ?? new Uri("https://graph.microsoft.com/v1.0/")
        };
    }

    /// <summary>
    /// Initializes the client using an externally managed <see cref="HttpClient"/>.
    /// </summary>
    /// <remarks>
    /// If <paramref name="client"/> does not specify <see cref="HttpClient.BaseAddress"/>, it will be set to the Graph v1.0 endpoint
    /// (or <paramref name="baseAddress"/> if provided).
    /// </remarks>
    /// <param name="client">HTTP client to use for requests.</param>
    /// <param name="refreshToken">Optional delegate used to refresh an access token when a request returns 401/403.</param>
    /// <param name="credential">Optional OAuth credential holding an access token.</param>
    /// <param name="baseAddress">Optional Graph base address used when <paramref name="client"/> has no base address configured.</param>
    public GraphApiClient(
        HttpClient client,
        Func<CancellationToken, Task<string>>? refreshToken = null,
        OAuthCredential? credential = null,
        Uri? baseAddress = null) {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _refreshToken = refreshToken;
        _credential = credential;
        if (_client.BaseAddress == null) {
            _client.BaseAddress = baseAddress ?? new Uri("https://graph.microsoft.com/v1.0/");
        }
    }

    /// <inheritdoc />
    public void Dispose() {
        if (_disposed) {
            return;
        }
        _client.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task ThrowIfAuthErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken) {
        if (response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden) {
            if (_refreshToken != null) {
                string token = await _refreshToken(cancellationToken).ConfigureAwait(false);
                if (_credential != null) {
                    _credential.AccessToken = token;
                }
            }
#if NET5_0_OR_GREATER
            string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            throw new GraphApiException(response.StatusCode, "Graph authentication failed.", content);
        }
    }

    private static TimeSpan? TryGetRetryAfter(HttpResponseMessage response) {
        if (response.Headers?.RetryAfter == null) {
            return null;
        }
        if (response.Headers.RetryAfter.Delta.HasValue) {
            return response.Headers.RetryAfter.Delta.Value;
        }
        if (response.Headers.RetryAfter.Date.HasValue) {
            var utc = response.Headers.RetryAfter.Date.Value.ToUniversalTime();
            var now = DateTimeOffset.UtcNow;
            if (utc > now) {
                return utc - now;
            }
        }
        return null;
    }

    /// <summary>
    /// Creates a webhook subscription.
    /// </summary>
    public async Task<GraphSubscription> CreateSubscriptionAsync(GraphCreateSubscriptionRequest request, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.Resource)) {
            throw new ArgumentException("Resource is required.", nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.ChangeType)) {
            throw new ArgumentException("ChangeType is required.", nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.NotificationUrl)) {
            throw new ArgumentException("NotificationUrl is required.", nameof(request));
        }
        if (request.ExpirationDateTime == default) {
            throw new ArgumentException("ExpirationDateTime is required.", nameof(request));
        }

        var json = JsonSerializer.Serialize(request, MailozaurrJsonContext.Default.GraphCreateSubscriptionRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var req = new HttpRequestMessage(HttpMethod.Post, "subscriptions") { Content = content };
        ApplyAuthHeader(req);
        using var response = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!response.IsSuccessStatusCode) {
            throw new GraphApiException(response.StatusCode, $"Graph subscription create failed ({(int)response.StatusCode}).", body, TryGetRetryAfter(response));
        }

        GraphSubscription? result;
        try {
            result = JsonSerializer.Deserialize(body, MailozaurrJsonContext.Default.GraphSubscription);
        } catch (JsonException ex) {
            throw new InvalidDataException("Failed to parse Graph subscription create response.", ex);
        }
        if (result is null) {
            throw new InvalidDataException("Graph returned an invalid subscription response.");
        }
        return result;
    }

    /// <summary>
    /// Renews a webhook subscription by updating its expiration.
    /// </summary>
    public async Task<GraphSubscription> RenewSubscriptionAsync(string subscriptionId, DateTimeOffset expirationDateTime, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(subscriptionId)) {
            throw new ArgumentException("subscriptionId is required.", nameof(subscriptionId));
        }

        var encodedId = Uri.EscapeDataString(subscriptionId.Trim());
        var request = new GraphRenewSubscriptionRequest { ExpirationDateTime = expirationDateTime };
        var json = JsonSerializer.Serialize(request, MailozaurrJsonContext.Default.GraphRenewSubscriptionRequest);
        var requestUri = _client.BaseAddress != null
            ? new Uri(_client.BaseAddress, $"subscriptions/{encodedId}")
            : new Uri($"subscriptions/{encodedId}", UriKind.Relative);
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), requestUri) {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        ApplyAuthHeader(req);
        using var response = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!response.IsSuccessStatusCode) {
            throw new GraphApiException(response.StatusCode, $"Graph subscription renew failed ({(int)response.StatusCode}).", body, TryGetRetryAfter(response));
        }

        GraphSubscription? result;
        try {
            result = JsonSerializer.Deserialize(body, MailozaurrJsonContext.Default.GraphSubscription);
        } catch (JsonException ex) {
            throw new InvalidDataException("Failed to parse Graph subscription renew response.", ex);
        }
        if (result is null) {
            throw new InvalidDataException("Graph returned an invalid subscription response.");
        }
        return result;
    }

    /// <summary>
    /// Deletes a webhook subscription.
    /// </summary>
    public async Task DeleteSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(subscriptionId)) {
            throw new ArgumentException("subscriptionId is required.", nameof(subscriptionId));
        }
        var encodedId = Uri.EscapeDataString(subscriptionId.Trim());
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"subscriptions/{encodedId}");
        ApplyAuthHeader(req);
        using var response = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!response.IsSuccessStatusCode) {
            throw new GraphApiException(response.StatusCode, $"Graph subscription delete failed ({(int)response.StatusCode}).", body, TryGetRetryAfter(response));
        }
    }

    /// <summary>
    /// Lists webhook subscriptions for the current token context.
    /// </summary>
    public async Task<IReadOnlyList<GraphSubscription>> ListSubscriptionsAsync(CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        using var req = new HttpRequestMessage(HttpMethod.Get, "subscriptions");
        ApplyAuthHeader(req);
        using var response = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!response.IsSuccessStatusCode) {
            throw new GraphApiException(response.StatusCode, $"Graph subscriptions list failed ({(int)response.StatusCode}).", body, TryGetRetryAfter(response));
        }
        GraphSubscriptionListResponse? result;
        try {
            result = JsonSerializer.Deserialize(body, MailozaurrJsonContext.Default.GraphSubscriptionListResponse);
        } catch (JsonException ex) {
            throw new InvalidDataException("Failed to parse Graph subscriptions list response.", ex);
        }
        return (IReadOnlyList<GraphSubscription>?)result?.Value ?? Array.Empty<GraphSubscription>();
    }

    private static string BuildUserSegment(string userId) {
        var u = (userId ?? string.Empty).Trim();
        if (u.Length == 0 || u.Equals("me", StringComparison.OrdinalIgnoreCase)) {
            return "me";
        }
        return "users/" + Uri.EscapeDataString(u);
    }

    private static int ClampInt(int value, int min, int max) {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static string EscapeODataStringLiteral(string value) => value.Replace("'", "''");

    private static string? TryGetString(JsonElement obj, string propertyName) {
        if (obj.ValueKind != JsonValueKind.Object) {
            return null;
        }
        if (!obj.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.String) {
            return null;
        }
        var s = el.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static int? TryGetInt(JsonElement obj, string propertyName) {
        if (obj.ValueKind != JsonValueKind.Object) {
            return null;
        }
        if (!obj.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.Number) {
            return null;
        }
        return el.TryGetInt32(out var v) ? v : null;
    }

    private static bool? TryGetBool(JsonElement obj, string propertyName) {
        if (obj.ValueKind != JsonValueKind.Object) {
            return null;
        }
        if (!obj.TryGetProperty(propertyName, out var el)) {
            return null;
        }
        if (el.ValueKind == JsonValueKind.True) {
            return true;
        }
        if (el.ValueKind == JsonValueKind.False) {
            return false;
        }
        return null;
    }

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement obj, string propertyName) {
        var s = TryGetString(obj, propertyName);
        if (string.IsNullOrWhiteSpace(s)) {
            return null;
        }
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt)
            ? dt
            : null;
    }

    private static GraphEmailAddress? TryParseEmailAddress(JsonElement recipient) {
        if (recipient.ValueKind != JsonValueKind.Object) {
            return null;
        }
        if (!recipient.TryGetProperty("emailAddress", out var emailAddress) || emailAddress.ValueKind != JsonValueKind.Object) {
            return null;
        }
        if (!emailAddress.TryGetProperty("address", out var addressEl) || addressEl.ValueKind != JsonValueKind.String) {
            return null;
        }
        var address = addressEl.GetString();
        if (address == null) {
            return null;
        }
        address = address.Trim();
        if (address.Length == 0) {
            return null;
        }
        return new GraphEmailAddress { Email = new GraphEmail { Address = address } };
    }

    private static List<GraphEmailAddress>? TryParseEmailAddressList(JsonElement obj, string propertyName) {
        if (obj.ValueKind != JsonValueKind.Object) {
            return null;
        }
        if (!obj.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.Array) {
            return null;
        }
        var list = new List<GraphEmailAddress>();
        foreach (var item in el.EnumerateArray()) {
            var addr = TryParseEmailAddress(item);
            if (addr != null) {
                list.Add(addr);
            }
        }
        return list.Count == 0 ? null : list;
    }

    private static GraphMailMessage? TryParseMailMessage(JsonElement obj) {
        if (obj.ValueKind != JsonValueKind.Object) {
            return null;
        }
        var idRaw = TryGetString(obj, "id");
        if (idRaw == null) {
            return null;
        }
        var id = idRaw.Trim();
        if (id.Length == 0) {
            return null;
        }
        var msg = new GraphMailMessage {
            Id = id,
            Subject = TryGetString(obj, "subject"),
            ReceivedDateTime = TryGetDateTimeOffset(obj, "receivedDateTime"),
            InternetMessageId = TryGetString(obj, "internetMessageId"),
            HasAttachments = TryGetBool(obj, "hasAttachments"),
            IsRead = TryGetBool(obj, "isRead"),
            ConversationId = TryGetString(obj, "conversationId")
        };

        if (obj.TryGetProperty("from", out var fromEl)) {
            msg.From = TryParseEmailAddress(fromEl);
        }
        msg.ToRecipients = TryParseEmailAddressList(obj, "toRecipients");

        if (obj.TryGetProperty("flag", out var flagEl) && flagEl.ValueKind == JsonValueKind.Object) {
            var status = TryGetString(flagEl, "flagStatus");
            if (status != null) {
                var trimmed = status.Trim();
                if (trimmed.Length > 0) {
                    msg.Flag = new GraphMailMessageFlag { FlagStatus = trimmed };
                }
            }
        }
        return msg;
    }

    private static GraphMailFolder? TryParseMailFolder(JsonElement obj) {
        if (obj.ValueKind != JsonValueKind.Object) {
            return null;
        }
        var idRaw = TryGetString(obj, "id");
        if (idRaw == null) {
            return null;
        }
        var id = idRaw.Trim();
        if (id.Length == 0) {
            return null;
        }
        return new GraphMailFolder {
            Id = id,
            DisplayName = (TryGetString(obj, "displayName") ?? string.Empty).Trim(),
            ParentFolderId = TryGetString(obj, "parentFolderId")?.Trim(),
            ChildFolderCount = TryGetInt(obj, "childFolderCount"),
            WellKnownName = TryGetString(obj, "wellKnownName")?.Trim(),
            TotalItemCount = TryGetInt(obj, "totalItemCount"),
            UnreadItemCount = TryGetInt(obj, "unreadItemCount")
        };
    }

    /// <summary>
    /// Lists mail folders for the given user, recursively expanding child folders.
    /// </summary>
    /// <remarks>
    /// This performs best-effort traversal with safeguards to prevent infinite loops in pathological cases.
    /// </remarks>
    public async Task<IReadOnlyList<GraphMailFolder>> ListMailFoldersRecursiveAsync(
        string userId = "me",
        int top = 200,
        string? select = "id,displayName,parentFolderId,childFolderCount,wellKnownName,totalItemCount,unreadItemCount",
        int maxRequests = 250,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        var safeTop = ClampInt(top, 1, 999);
        var safeMax = ClampInt(maxRequests, 1, 5000);
        var userSegment = BuildUserSegment(userId);

        var foldersById = new Dictionary<string, GraphMailFolder>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        var initial = new StringBuilder();
        initial.Append(userSegment).Append("/mailFolders?$top=").Append(safeTop.ToString(CultureInfo.InvariantCulture));
        var selectValue = select == null ? null : select.Trim();
        if (selectValue != null && selectValue.Length > 0) {
            initial.Append("&$select=").Append(Uri.EscapeDataString(selectValue));
        }
        pending.Enqueue(initial.ToString());

        var processed = 0;
        while (pending.Count > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            if (processed++ >= safeMax) {
                break;
            }

            var url = pending.Dequeue();
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyAuthHeader(req);
            using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            if (!resp.IsSuccessStatusCode) {
                throw new GraphApiException(resp.StatusCode, $"Graph mailFolders list failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array) {
                foreach (var item in value.EnumerateArray()) {
                    var folder = TryParseMailFolder(item);
                    if (folder == null || string.IsNullOrWhiteSpace(folder.Id)) {
                        continue;
                    }
                    foldersById[folder.Id] = folder;

                    if (folder.ChildFolderCount.HasValue && folder.ChildFolderCount.Value > 0) {
                        var childUrl = new StringBuilder();
                        childUrl.Append(userSegment)
                            .Append("/mailFolders/")
                            .Append(Uri.EscapeDataString(folder.Id))
                            .Append("/childFolders?$top=")
                            .Append(safeTop.ToString(CultureInfo.InvariantCulture));
                        if (selectValue != null && selectValue.Length > 0) {
                            childUrl.Append("&$select=").Append(Uri.EscapeDataString(selectValue));
                        }
                        pending.Enqueue(childUrl.ToString());
                    }
                }
            }

            if (doc.RootElement.TryGetProperty("@odata.nextLink", out var next) && next.ValueKind == JsonValueKind.String) {
                var nextLink = next.GetString();
                if (nextLink != null && nextLink.Trim().Length > 0) {
                    pending.Enqueue(nextLink);
                }
            }
        }

        var output = new List<GraphMailFolder>(foldersById.Count);
        output.AddRange(foldersById.Values);
        output.Sort(static (a, b) => {
            var r = string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (r != 0) return r;
            return string.Compare(a.Id, b.Id, StringComparison.Ordinal);
        });
        return output;
    }

    /// <summary>
    /// Gets a single mail folder record.
    /// </summary>
    public async Task<GraphMailFolder> GetMailFolderAsync(
        string folderIdOrWellKnownName,
        string userId = "me",
        string? select = "id,displayName,parentFolderId,childFolderCount,wellKnownName,totalItemCount,unreadItemCount",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(folderIdOrWellKnownName)) {
            throw new ArgumentException("folderIdOrWellKnownName is required.", nameof(folderIdOrWellKnownName));
        }

        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(folderIdOrWellKnownName.Trim());
        var url = new StringBuilder();
        url.Append(userSegment).Append("/mailFolders/").Append(selector);
        var selectValue = select == null ? null : select.Trim();
        if (selectValue != null && selectValue.Length > 0) {
            url.Append("?$select=").Append(Uri.EscapeDataString(selectValue));
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, url.ToString());
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph mailFolder get failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

        using var doc = JsonDocument.Parse(body);
        var folder = TryParseMailFolder(doc.RootElement);
        if (folder is null) {
            throw new InvalidDataException("Graph returned an invalid mail folder response.");
        }
        return folder;
    }

    /// <summary>
    /// Lists messages within a mail folder.
    /// </summary>
    public async Task<GraphPage<GraphMailMessage>> ListMessagesAsync(
        string folderIdOrWellKnownName,
        string userId = "me",
        int top = 100,
        int? skip = null,
        string? select = "id,subject,receivedDateTime,from,toRecipients,internetMessageId,hasAttachments,isRead,flag,conversationId",
        string? orderBy = "receivedDateTime desc",
        string? filter = null,
        string? search = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(folderIdOrWellKnownName)) {
            throw new ArgumentException("folderIdOrWellKnownName is required.", nameof(folderIdOrWellKnownName));
        }

        var safeTop = ClampInt(top, 1, 999);
        var userSegment = BuildUserSegment(userId);
        var folderSelector = Uri.EscapeDataString(folderIdOrWellKnownName.Trim());

        var url = new StringBuilder();
        url.Append(userSegment).Append("/mailFolders/").Append(folderSelector).Append("/messages");
        url.Append("?$top=").Append(safeTop.ToString(CultureInfo.InvariantCulture));
        if (skip.HasValue && skip.Value > 0) {
            url.Append("&$skip=").Append(skip.Value.ToString(CultureInfo.InvariantCulture));
        }
        var orderByValue = orderBy == null ? null : orderBy.Trim();
        if (orderByValue != null && orderByValue.Length > 0) {
            url.Append("&$orderby=").Append(Uri.EscapeDataString(orderByValue));
        }
        var selectValue = select == null ? null : select.Trim();
        if (selectValue != null && selectValue.Length > 0) {
            url.Append("&$select=").Append(Uri.EscapeDataString(selectValue));
        }
        var filterValue = filter == null ? null : filter.Trim();
        if (filterValue != null && filterValue.Length > 0) {
            url.Append("&$filter=").Append(Uri.EscapeDataString(filterValue));
        }
        var searchValue = search == null ? null : search.Trim();
        if (searchValue != null && searchValue.Length > 0) {
            var sanitized = searchValue.Replace("\"", string.Empty).Trim();
            if (sanitized.Length > 0) {
                url.Append("&$search=").Append(Uri.EscapeDataString("\"" + sanitized + "\""));
            }
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, url.ToString());
        ApplyAuthHeader(req);
        if (searchValue != null && searchValue.Length > 0) {
            req.Headers.TryAddWithoutValidation("ConsistencyLevel", "eventual");
            req.Headers.TryAddWithoutValidation("Prefer", "HonorNonIndexedQueriesWarning=true");
        }
        using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph messages list failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

        var items = new List<GraphMailMessage>();
        string? nextLink = null;
        using (var doc = JsonDocument.Parse(body)) {
            if (doc.RootElement.TryGetProperty("@odata.nextLink", out var next) && next.ValueKind == JsonValueKind.String) {
                nextLink = next.GetString();
            }
            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array) {
                foreach (var it in value.EnumerateArray()) {
                    var msg = TryParseMailMessage(it);
                    if (msg != null) {
                        items.Add(msg);
                    }
                }
            }
        }

        return new GraphPage<GraphMailMessage>(items, nextLink);
    }

    /// <summary>
    /// Lists message ids for a Graph conversation.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListConversationMessageIdsAsync(
        string conversationId,
        string userId = "me",
        int top = 100,
        int maxPages = 25,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(conversationId)) {
            throw new ArgumentException("conversationId is required.", nameof(conversationId));
        }

        var safeTop = ClampInt(top, 1, 999);
        var safeMaxPages = ClampInt(maxPages, 1, 500);
        var userSegment = BuildUserSegment(userId);

        var ids = new List<string>();
        var pages = 0;
        var filter = "conversationId eq '" + EscapeODataStringLiteral(conversationId.Trim()) + "'";
        var url = new StringBuilder();
        url.Append(userSegment).Append("/messages?$select=id&$top=").Append(safeTop.ToString(CultureInfo.InvariantCulture));
        url.Append("&$filter=").Append(Uri.EscapeDataString(filter));

        string nextUrl = url.ToString();
        while (pages++ < safeMaxPages) {
            cancellationToken.ThrowIfCancellationRequested();
            using var req = new HttpRequestMessage(HttpMethod.Get, nextUrl);
            ApplyAuthHeader(req);
            using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            if (!resp.IsSuccessStatusCode) {
                throw new GraphApiException(resp.StatusCode, $"Graph conversation list failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array) {
                foreach (var it in value.EnumerateArray()) {
                    var id = TryGetString(it, "id");
                    if (id != null) {
                        var trimmed = id.Trim();
                        if (trimmed.Length > 0) {
                            ids.Add(trimmed);
                        }
                    }
                }
            }
            if (doc.RootElement.TryGetProperty("@odata.nextLink", out var next) && next.ValueKind == JsonValueKind.String) {
                var link = next.GetString();
                if (link != null) {
                    var trimmed = link.Trim();
                    if (trimmed.Length > 0) {
                        nextUrl = trimmed;
                        continue;
                    }
                }
            }
            break;
        }

        return ids;
    }

    /// <summary>
    /// Lists message metadata for a Graph conversation.
    /// </summary>
    public async Task<IReadOnlyList<GraphMailMessage>> ListConversationMessagesAsync(
        string conversationId,
        string userId = "me",
        int top = 100,
        int maxPages = 25,
        string? select = "id,subject,receivedDateTime,from,toRecipients,internetMessageId,hasAttachments,isRead,flag,conversationId",
        string? orderBy = "receivedDateTime desc",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(conversationId)) {
            throw new ArgumentException("conversationId is required.", nameof(conversationId));
        }

        var safeTop = ClampInt(top, 1, 999);
        var safeMaxPages = ClampInt(maxPages, 1, 500);
        var userSegment = BuildUserSegment(userId);

        var messages = new List<GraphMailMessage>();
        var pages = 0;
        var filter = "conversationId eq '" + EscapeODataStringLiteral(conversationId.Trim()) + "'";
        var url = new StringBuilder();
        url.Append(userSegment).Append("/messages?$top=").Append(safeTop.ToString(CultureInfo.InvariantCulture));
        url.Append("&$filter=").Append(Uri.EscapeDataString(filter));

        var selectValue = select == null ? null : select.Trim();
        if (selectValue != null && selectValue.Length > 0) {
            url.Append("&$select=").Append(Uri.EscapeDataString(selectValue));
        }

        var orderByValue = orderBy == null ? null : orderBy.Trim();
        if (orderByValue != null && orderByValue.Length > 0) {
            url.Append("&$orderby=").Append(Uri.EscapeDataString(orderByValue));
        }

        string nextUrl = url.ToString();
        while (pages++ < safeMaxPages) {
            cancellationToken.ThrowIfCancellationRequested();
            using var req = new HttpRequestMessage(HttpMethod.Get, nextUrl);
            ApplyAuthHeader(req);
            using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            if (!resp.IsSuccessStatusCode) {
                throw new GraphApiException(resp.StatusCode, $"Graph conversation list failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array) {
                foreach (var it in value.EnumerateArray()) {
                    var msg = TryParseMailMessage(it);
                    if (msg != null) {
                        messages.Add(msg);
                    }
                }
            }

            if (doc.RootElement.TryGetProperty("@odata.nextLink", out var next) && next.ValueKind == JsonValueKind.String) {
                var link = next.GetString();
                if (link != null) {
                    var trimmed = link.Trim();
                    if (trimmed.Length > 0) {
                        nextUrl = trimmed;
                        continue;
                    }
                }
            }
            break;
        }

        return messages;
    }

    /// <summary>
    /// Gets message metadata.
    /// </summary>
    public async Task<GraphMailMessage> GetMessageAsync(
        string messageId,
        string userId = "me",
        string? select = "id,subject,receivedDateTime,from,toRecipients,internetMessageId,hasAttachments,isRead,flag,conversationId",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        var url = new StringBuilder();
        url.Append(userSegment).Append("/messages/").Append(selector);
        var selectValue = select == null ? null : select.Trim();
        if (selectValue != null && selectValue.Length > 0) {
            url.Append("?$select=").Append(Uri.EscapeDataString(selectValue));
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, url.ToString());
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph message get failed for messageId '{messageId.Trim()}' ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

        using var doc = JsonDocument.Parse(body);
        var msg = TryParseMailMessage(doc.RootElement);
        if (msg is null) {
            throw new InvalidDataException("Graph returned an invalid message response.");
        }
        return msg;
    }

    /// <summary>
    /// Downloads the message MIME content via the <c>/$value</c> endpoint.
    /// </summary>
    public async Task<byte[]> GetMessageMimeAsync(
        string messageId,
        string userId = "me",
        int maxBytes = 25 * 1024 * 1024,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        if (maxBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "maxBytes must be > 0.");
        }
        const int hardLimitBytes = 256 * 1024 * 1024;
        if (maxBytes > hardLimitBytes) {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), $"maxBytes must be <= {hardLimitBytes}.");
        }

        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        var url = userSegment + "/messages/" + selector + "/$value";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) {
#if NET5_0_OR_GREATER
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            throw new GraphApiException(resp.StatusCode, $"Graph MIME fetch failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

#if NET5_0_OR_GREATER
        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
#if NET5_0_OR_GREATER
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
#else
            var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
#endif
            if (read <= 0) {
                break;
            }
            if (ms.Length + read > maxBytes) {
                throw new InvalidDataException($"Graph MIME content exceeds {maxBytes} bytes.");
            }
#if NET5_0_OR_GREATER
            await ms.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
#else
            ms.Write(buffer, 0, read);
#endif
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Creates a message draft, optionally under a specific folder.
    /// </summary>
    public async Task<GraphMessage> CreateMessageAsync(
        GraphMessage message,
        string userId = "me",
        string? folderIdOrWellKnownName = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }

        var userSegment = BuildUserSegment(userId);
        string? trimmedFolderId = null;
        if (folderIdOrWellKnownName != null) {
            var candidate = folderIdOrWellKnownName.Trim();
            if (candidate.Length > 0) {
                trimmedFolderId = candidate;
            }
        }
        var url = trimmedFolderId == null
            ? userSegment + "/messages"
            : userSegment + "/mailFolders/" + Uri.EscapeDataString(trimmedFolderId) + "/messages";
        var payload = JsonSerializer.Serialize(message, MailozaurrJsonContext.Default.GraphMessage);
        using var req = new HttpRequestMessage(HttpMethod.Post, url) {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph message create failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

        GraphMessage? created;
        try {
            created = JsonSerializer.Deserialize(body, MailozaurrJsonContext.Default.GraphMessage);
        } catch (JsonException ex) {
            throw new InvalidDataException("Failed to parse Graph message create response.", ex);
        }
        if (created == null || string.IsNullOrWhiteSpace(created.Id)) {
            throw new InvalidDataException("Graph returned an invalid created message response.");
        }
        return created;
    }

    /// <summary>
    /// Sends an existing draft message.
    /// </summary>
    public async Task SendDraftMessageAsync(
        string messageId,
        string userId = "me",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        using var req = new HttpRequestMessage(HttpMethod.Post, userSegment + "/messages/" + selector + "/send");
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph draft send failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }
    }

    /// <summary>
    /// Creates attachment upload session for an existing draft message.
    /// </summary>
    public async Task<GraphUploadSessionResult> CreateAttachmentUploadSessionAsync(
        string messageId,
        GraphAttachmentItem attachmentItem,
        string userId = "me",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        if (attachmentItem == null) {
            throw new ArgumentNullException(nameof(attachmentItem));
        }
        if (string.IsNullOrWhiteSpace(attachmentItem.Name)) {
            throw new ArgumentException("attachmentItem.Name is required.", nameof(attachmentItem));
        }
        if (attachmentItem.Size <= 0) {
            throw new ArgumentOutOfRangeException(nameof(attachmentItem), "attachmentItem.Size must be > 0.");
        }

        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        var url = userSegment + "/messages/" + selector + "/attachments/createUploadSession";

        var payload = BuildCreateUploadSessionPayload(attachmentItem);
        using var req = new HttpRequestMessage(HttpMethod.Post, url) {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph upload session create failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

        GraphUploadSessionResult? result;
        try {
            result = JsonSerializer.Deserialize(body, MailozaurrJsonContext.Default.GraphUploadSessionResult);
        } catch (JsonException ex) {
            throw new InvalidDataException("Failed to parse Graph upload session response.", ex);
        }
        if (result == null || string.IsNullOrWhiteSpace(result.UploadUrl)) {
            throw new InvalidDataException("Graph returned an invalid upload session response.");
        }
        return result;
    }

    /// <summary>
    /// Uploads one attachment chunk to a Graph upload session URL.
    /// </summary>
    public async Task UploadAttachmentChunkAsync(
        string uploadUrl,
        byte[] chunk,
        long startInclusive,
        long endInclusive,
        long totalLength,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(uploadUrl)) {
            throw new ArgumentException("uploadUrl is required.", nameof(uploadUrl));
        }
        if (chunk == null) {
            throw new ArgumentNullException(nameof(chunk));
        }
        if (chunk.Length == 0) {
            throw new ArgumentException("chunk must not be empty.", nameof(chunk));
        }

        using var req = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
        req.Content = new ByteArrayContent(chunk);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        req.Content.Headers.ContentRange = new ContentRangeHeaderValue(startInclusive, endInclusive, totalLength);

        using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (resp.IsSuccessStatusCode || (int)resp.StatusCode == 202) {
            return;
        }

        throw new GraphApiException(resp.StatusCode, $"Graph attachment chunk upload failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
    }

    private static string BuildCreateUploadSessionPayload(GraphAttachmentItem attachmentItem) {
        static string JsonString(string value) =>
            "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        var sb = new StringBuilder();
        sb.Append("{\"attachmentItem\":{");
        sb.Append("\"attachmentType\":").Append(JsonString(string.IsNullOrWhiteSpace(attachmentItem.AttachmentType) ? "file" : attachmentItem.AttachmentType.Trim())).Append(',');
        sb.Append("\"name\":").Append(JsonString(attachmentItem.Name.Trim())).Append(',');
        sb.Append("\"size\":").Append(attachmentItem.Size.ToString(CultureInfo.InvariantCulture));
        string? contentType = null;
        if (attachmentItem.ContentType != null) {
            var candidate = attachmentItem.ContentType.Trim();
            if (candidate.Length > 0) {
                contentType = candidate;
            }
        }
        if (contentType != null) {
            sb.Append(",\"contentType\":").Append(JsonString(contentType));
        }
        if (attachmentItem.IsInline.HasValue && attachmentItem.IsInline.Value) {
            sb.Append(",\"isInline\":true");
            string? contentId = null;
            if (attachmentItem.ContentId != null) {
                var candidate = attachmentItem.ContentId.Trim();
                if (candidate.Length > 0) {
                    contentId = candidate;
                }
            }
            if (contentId != null) {
                sb.Append(",\"contentId\":").Append(JsonString(contentId));
            }
        }
        sb.Append("}}");
        return sb.ToString();
    }

    /// <summary>
    /// Retrieves a delta page for messages in a folder.
    /// </summary>
    /// <remarks>
    /// When <paramref name="cursor"/> is empty, starts a new delta query for the folder.
    /// Otherwise, continues from a previously returned nextLink/deltaLink.
    /// </remarks>
    public async Task<GraphDeltaPage<GraphMailMessage>> DeltaMessagesAsync(
        string folderIdOrWellKnownName,
        string? cursor = null,
        string userId = "me",
        int top = 100,
        string? select = "id,subject,receivedDateTime,from,toRecipients,internetMessageId,hasAttachments,isRead,flag,conversationId",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(folderIdOrWellKnownName)) {
            throw new ArgumentException("folderIdOrWellKnownName is required.", nameof(folderIdOrWellKnownName));
        }

        var safeTop = ClampInt(top, 1, 999);
        var userSegment = BuildUserSegment(userId);

        var url = (cursor ?? string.Empty).Trim();
        if (url.Length == 0) {
            var folderSelector = Uri.EscapeDataString(folderIdOrWellKnownName.Trim());
            var sb = new StringBuilder();
            sb.Append(userSegment).Append("/mailFolders/").Append(folderSelector).Append("/messages/delta");
            sb.Append("?$top=").Append(safeTop.ToString(CultureInfo.InvariantCulture));
            var selectValue = select == null ? null : select.Trim();
            if (selectValue != null && selectValue.Length > 0) {
                sb.Append("&$select=").Append(Uri.EscapeDataString(selectValue));
            }
            url = sb.ToString();
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph delta failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

        var upserts = new List<GraphMailMessage>();
        var deletedIds = new List<string>();
        string? nextLink = null;
        string? deltaLink = null;

        using (var doc = JsonDocument.Parse(body)) {
            if (doc.RootElement.TryGetProperty("@odata.nextLink", out var next) && next.ValueKind == JsonValueKind.String) {
                nextLink = next.GetString();
            }
            if (doc.RootElement.TryGetProperty("@odata.deltaLink", out var delta) && delta.ValueKind == JsonValueKind.String) {
                deltaLink = delta.GetString();
            }
            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array) {
                foreach (var item in value.EnumerateArray()) {
                    var id = TryGetString(item, "id");
                    if (id == null) {
                        continue;
                    }
                    var trimmedId = id.Trim();
                    if (trimmedId.Length == 0) {
                        continue;
                    }
                    if (item.TryGetProperty("@removed", out _)) {
                        deletedIds.Add(trimmedId);
                        continue;
                    }
                    var msg = TryParseMailMessage(item);
                    if (msg != null) {
                        upserts.Add(msg);
                    }
                }
            }
        }

        return new GraphDeltaPage<GraphMailMessage>(upserts, nextLink, deltaLink, deletedIds);
    }

    /// <summary>
    /// Moves a message to the specified destination folder id.
    /// </summary>
    public async Task MoveMessageAsync(
        string messageId,
        string destinationId,
        string userId = "me",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        if (string.IsNullOrWhiteSpace(destinationId)) {
            throw new ArgumentException("destinationId is required.", nameof(destinationId));
        }

        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        var json = JsonSerializer.Serialize(new GraphDestinationRequest { DestinationId = destinationId.Trim() }, MailozaurrJsonContext.Default.GraphDestinationRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var req = new HttpRequestMessage(HttpMethod.Post, userSegment + "/messages/" + selector + "/move") { Content = content };
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph move failed for messageId '{messageId.Trim()}' ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }
    }

    /// <summary>
    /// Deletes a message.
    /// </summary>
    public async Task DeleteMessageAsync(string messageId, string userId = "me", CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        using var req = new HttpRequestMessage(HttpMethod.Delete, userSegment + "/messages/" + selector);
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph delete failed for messageId '{messageId.Trim()}' ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }
    }

    /// <summary>
    /// Sets the read state of a message.
    /// </summary>
    public async Task SetMessageIsReadAsync(
        string messageId,
        bool isRead,
        string userId = "me",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        var json = JsonSerializer.Serialize(new GraphMarkReadRequest { IsRead = isRead }, MailozaurrJsonContext.Default.GraphMarkReadRequest);
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), userSegment + "/messages/" + selector) {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph set-isRead failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }
    }

    /// <summary>
    /// Sets the flagged state of a message.
    /// </summary>
    public async Task SetMessageFlaggedAsync(
        string messageId,
        bool flagged,
        string userId = "me",
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        var userSegment = BuildUserSegment(userId);
        var selector = Uri.EscapeDataString(messageId.Trim());
        var status = flagged ? "flagged" : "notFlagged";
        var json = JsonSerializer.Serialize(
            new GraphSetFlagRequest { Flag = new GraphSetFlagRequestFlag { FlagStatus = status } },
            MailozaurrJsonContext.Default.GraphSetFlagRequest);
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), userSegment + "/messages/" + selector) {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph set-flag failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }
    }

    /// <summary>
    /// Moves many messages using Graph batch requests.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> BatchMoveMessagesAsync(
        IEnumerable<string> messageIds,
        string destinationFolderId,
        string userId = "me",
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }
        if (string.IsNullOrWhiteSpace(destinationFolderId)) {
            throw new ArgumentException("destinationFolderId is required.", nameof(destinationFolderId));
        }

        var ids = NormalizeBulkIds(messageIds);
        if (ids.Count == 0) {
            return Array.Empty<GraphBulkOperationResult>();
        }

        var userSegment = BuildUserSegment(userId);
        var batch = ClampInt(batchSize, 1, 20);
        var payloadJson = JsonSerializer.Serialize(
            new GraphDestinationRequest { DestinationId = destinationFolderId.Trim() },
            MailozaurrJsonContext.Default.GraphDestinationRequest);
        using var bodyDoc = JsonDocument.Parse(payloadJson);
        var body = bodyDoc.RootElement.Clone();

        return await ExecuteMessageBatchAsync(
            ids,
            batch,
            messageId => new GraphBatchRequest {
                Method = GraphHttpMethod.POST,
                Url = userSegment + "/messages/" + Uri.EscapeDataString(messageId) + "/move",
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json" },
                Body = body
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes many messages using Graph batch requests.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> BatchDeleteMessagesAsync(
        IEnumerable<string> messageIds,
        string userId = "me",
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        var ids = NormalizeBulkIds(messageIds);
        if (ids.Count == 0) {
            return Array.Empty<GraphBulkOperationResult>();
        }

        var userSegment = BuildUserSegment(userId);
        var batch = ClampInt(batchSize, 1, 20);
        return await ExecuteMessageBatchAsync(
            ids,
            batch,
            messageId => new GraphBatchRequest {
                Method = GraphHttpMethod.DELETE,
                Url = userSegment + "/messages/" + Uri.EscapeDataString(messageId)
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets read/unread state for many messages using Graph batch requests.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> BatchSetMessagesIsReadAsync(
        IEnumerable<string> messageIds,
        bool isRead,
        string userId = "me",
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        var ids = NormalizeBulkIds(messageIds);
        if (ids.Count == 0) {
            return Array.Empty<GraphBulkOperationResult>();
        }

        var userSegment = BuildUserSegment(userId);
        var batch = ClampInt(batchSize, 1, 20);
        var payloadJson = JsonSerializer.Serialize(
            new GraphMarkReadRequest { IsRead = isRead },
            MailozaurrJsonContext.Default.GraphMarkReadRequest);
        using var bodyDoc = JsonDocument.Parse(payloadJson);
        var body = bodyDoc.RootElement.Clone();

        return await ExecuteMessageBatchAsync(
            ids,
            batch,
            messageId => new GraphBatchRequest {
                Method = GraphHttpMethod.PATCH,
                Url = userSegment + "/messages/" + Uri.EscapeDataString(messageId),
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json" },
                Body = body
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets flagged/unflagged state for many messages using Graph batch requests.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> BatchSetMessagesFlaggedAsync(
        IEnumerable<string> messageIds,
        bool flagged,
        string userId = "me",
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        var ids = NormalizeBulkIds(messageIds);
        if (ids.Count == 0) {
            return Array.Empty<GraphBulkOperationResult>();
        }

        var userSegment = BuildUserSegment(userId);
        var batch = ClampInt(batchSize, 1, 20);
        var payloadJson = JsonSerializer.Serialize(
            new GraphSetFlagRequest { Flag = new GraphSetFlagRequestFlag { FlagStatus = flagged ? "flagged" : "notFlagged" } },
            MailozaurrJsonContext.Default.GraphSetFlagRequest);
        using var bodyDoc = JsonDocument.Parse(payloadJson);
        var body = bodyDoc.RootElement.Clone();

        return await ExecuteMessageBatchAsync(
            ids,
            batch,
            messageId => new GraphBatchRequest {
                Method = GraphHttpMethod.PATCH,
                Url = userSegment + "/messages/" + Uri.EscapeDataString(messageId),
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json" },
                Body = body
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves all messages in each conversation using Graph batch requests.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> BatchMoveConversationsAsync(
        IEnumerable<string> conversationIds,
        string destinationFolderId,
        string userId = "me",
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (conversationIds == null) {
            throw new ArgumentNullException(nameof(conversationIds));
        }
        if (string.IsNullOrWhiteSpace(destinationFolderId)) {
            throw new ArgumentException("destinationFolderId is required.", nameof(destinationFolderId));
        }

        var conversations = NormalizeBulkIds(conversationIds);
        if (conversations.Count == 0) {
            return Array.Empty<GraphBulkOperationResult>();
        }

        var output = new List<GraphBulkOperationResult>(conversations.Count);
        foreach (var conversationId in conversations) {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<string> messageIds;
            try {
                messageIds = await ListConversationMessageIdsAsync(conversationId, userId: userId, cancellationToken: cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                output.Add(new GraphBulkOperationResult {
                    Id = conversationId,
                    Ok = false,
                    Error = ex.Message
                });
                continue;
            }

            if (messageIds.Count == 0) {
                output.Add(new GraphBulkOperationResult { Id = conversationId, Ok = true });
                continue;
            }

            var moved = await BatchMoveMessagesAsync(
                messageIds,
                destinationFolderId,
                userId: userId,
                batchSize: batchSize,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var failed = FindFirstFailedBulkResult(moved);
            if (failed is not null) {
                output.Add(new GraphBulkOperationResult {
                    Id = conversationId,
                    Ok = false,
                    Error = failed.Error ?? "Graph conversation move failed."
                });
                continue;
            }
            output.Add(new GraphBulkOperationResult { Id = conversationId, Ok = true });
        }

        return output;
    }

    /// <summary>
    /// Deletes all messages in each conversation using Graph batch requests.
    /// </summary>
    public async Task<IReadOnlyList<GraphBulkOperationResult>> BatchDeleteConversationsAsync(
        IEnumerable<string> conversationIds,
        string userId = "me",
        int batchSize = 20,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (conversationIds == null) {
            throw new ArgumentNullException(nameof(conversationIds));
        }

        var conversations = NormalizeBulkIds(conversationIds);
        if (conversations.Count == 0) {
            return Array.Empty<GraphBulkOperationResult>();
        }

        var output = new List<GraphBulkOperationResult>(conversations.Count);
        foreach (var conversationId in conversations) {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<string> messageIds;
            try {
                messageIds = await ListConversationMessageIdsAsync(conversationId, userId: userId, cancellationToken: cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                output.Add(new GraphBulkOperationResult {
                    Id = conversationId,
                    Ok = false,
                    Error = ex.Message
                });
                continue;
            }

            if (messageIds.Count == 0) {
                output.Add(new GraphBulkOperationResult { Id = conversationId, Ok = true });
                continue;
            }

            var deleted = await BatchDeleteMessagesAsync(
                messageIds,
                userId: userId,
                batchSize: batchSize,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var failed = FindFirstFailedBulkResult(deleted);
            if (failed is not null) {
                output.Add(new GraphBulkOperationResult {
                    Id = conversationId,
                    Ok = false,
                    Error = failed.Error ?? "Graph conversation delete failed."
                });
                continue;
            }
            output.Add(new GraphBulkOperationResult { Id = conversationId, Ok = true });
        }

        return output;
    }

    /// <summary>
    /// Sends a Graph batch request using the current bearer token.
    /// </summary>
    public async Task<IReadOnlyList<GraphBatchResult>> SendBatchAsync(IEnumerable<GraphBatchRequest> requests, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (requests == null) {
            throw new ArgumentNullException(nameof(requests));
        }

        var payload = new GraphBatchPayload();
        var i = 0;
        foreach (var r in requests) {
            if (r == null) {
                continue;
            }
            var id = string.IsNullOrWhiteSpace(r.Id) ? (++i).ToString(CultureInfo.InvariantCulture) : r.Id.Trim();
            var url = (r.Url ?? string.Empty).Trim();
            if (url.Length == 0) {
                throw new ArgumentException("GraphBatchRequest.Url is required.", nameof(requests));
            }

            payload.Requests.Add(new GraphBatchRequestPayload {
                Id = id,
                Method = r.Method.ToString(),
                Url = url.TrimStart('/'),
                Headers = r.Headers,
                Body = r.Body
            });
        }

        var json = JsonSerializer.Serialize(payload, MailozaurrJsonContext.Default.GraphBatchPayload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var req = new HttpRequestMessage(HttpMethod.Post, "$batch") { Content = content };
        ApplyAuthHeader(req);
        using var resp = await _client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        await ThrowIfAuthErrorAsync(resp, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        if (!resp.IsSuccessStatusCode) {
            throw new GraphApiException(resp.StatusCode, $"Graph batch failed ({(int)resp.StatusCode}).", body, TryGetRetryAfter(resp));
        }

        var results = new List<GraphBatchResult>();
        using (var doc = JsonDocument.Parse(body)) {
            if (doc.RootElement.TryGetProperty("responses", out var responses) && responses.ValueKind == JsonValueKind.Array) {
                foreach (var item in responses.EnumerateArray()) {
                    var result = new GraphBatchResult();
                    if (item.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String) {
                        result.Id = idEl.GetString() ?? string.Empty;
                    }
                    if (item.TryGetProperty("status", out var statusEl) && statusEl.TryGetInt32(out var status)) {
                        result.Status = status;
                    }
                    if (item.TryGetProperty("headers", out var headersEl) && headersEl.ValueKind == JsonValueKind.Object) {
                        var h = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in headersEl.EnumerateObject()) {
                            if (prop.Value.ValueKind == JsonValueKind.String) {
                                h[prop.Name] = prop.Value.GetString() ?? string.Empty;
                            }
                        }
                        result.Headers = h;
                    }
                    if (item.TryGetProperty("body", out var bodyEl)) {
                        result.Body = bodyEl.Clone();
                    }
                    results.Add(result);
                }
            }
        }

        return results;
    }

    private static List<string> NormalizeBulkIds(IEnumerable<string> ids) {
        var output = new List<string>();
        foreach (var raw in ids) {
            if (string.IsNullOrWhiteSpace(raw)) {
                continue;
            }
            var id = raw.Trim();
            if (id.Length == 0) {
                continue;
            }
            output.Add(id);
        }
        return output;
    }

    private async Task<IReadOnlyList<GraphBulkOperationResult>> ExecuteMessageBatchAsync(
        List<string> messageIds,
        int batchSize,
        Func<string, GraphBatchRequest> requestFactory,
        CancellationToken cancellationToken) {
        var output = new List<GraphBulkOperationResult>(messageIds.Count);
        var chunkSize = ClampInt(batchSize, 1, 20);

        for (var i = 0; i < messageIds.Count; i += chunkSize) {
            cancellationToken.ThrowIfCancellationRequested();

            var count = Math.Min(chunkSize, messageIds.Count - i);
            var chunk = messageIds.GetRange(i, count);
            var subIds = new List<string>(chunk.Count);
            var requests = new List<GraphBatchRequest>(chunk.Count);
            for (var j = 0; j < chunk.Count; j++) {
                var subId = (j + 1).ToString(CultureInfo.InvariantCulture);
                subIds.Add(subId);
                var req = requestFactory(chunk[j]);
                req.Id = subId;
                requests.Add(req);
            }

            IReadOnlyList<GraphBatchResult> responses;
            try {
                responses = await SendBatchAsync(requests, cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                var err = ex.Message;
                foreach (var id in chunk) {
                    output.Add(new GraphBulkOperationResult { Id = id, Ok = false, Error = err });
                }
                continue;
            }

            var byId = new Dictionary<string, GraphBatchResult>(StringComparer.Ordinal);
            foreach (var response in responses) {
                if (response == null || string.IsNullOrWhiteSpace(response.Id)) {
                    continue;
                }
                byId[response.Id.Trim()] = response;
            }

            for (var j = 0; j < chunk.Count; j++) {
                var messageId = chunk[j];
                var subId = subIds[j];
                if (!byId.TryGetValue(subId, out var response)) {
                    output.Add(new GraphBulkOperationResult {
                        Id = messageId,
                        Ok = false,
                        Error = "Graph batch response missing for message id."
                    });
                    continue;
                }

                if (response.Status >= 200 && response.Status <= 299) {
                    output.Add(new GraphBulkOperationResult { Id = messageId, Ok = true });
                    continue;
                }

                output.Add(new GraphBulkOperationResult {
                    Id = messageId,
                    Ok = false,
                    Error = TryExtractBatchErrorMessage(response) ?? ("Graph batch request failed (" + response.Status.ToString(CultureInfo.InvariantCulture) + ").")
                });
            }
        }

        return output;
    }

    private static GraphBulkOperationResult? FindFirstFailedBulkResult(IReadOnlyList<GraphBulkOperationResult> results) {
        if (results == null) {
            return null;
        }
        foreach (var result in results) {
            if (result != null && !result.Ok) {
                return result;
            }
        }
        return null;
    }

    private static string? TryExtractBatchErrorMessage(GraphBatchResult response) {
        if (response?.Body == null) {
            return null;
        }

        var body = response.Body.Value;
        if (body.ValueKind != JsonValueKind.Object) {
            return null;
        }

        if (body.TryGetProperty("error", out var error)) {
            if (error.ValueKind == JsonValueKind.String) {
                var text = error.GetString();
                if (text != null) {
                    var trimmed = text.Trim();
                    if (trimmed.Length > 0) {
                        return trimmed;
                    }
                }
            }

            if (error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("message", out var messageEl) &&
                messageEl.ValueKind == JsonValueKind.String) {
                var msg = messageEl.GetString();
                if (msg != null) {
                    var trimmed = msg.Trim();
                    if (trimmed.Length > 0) {
                        return trimmed;
                    }
                }
            }
        }

        if (body.TryGetProperty("message", out var fallbackMessageEl) &&
            fallbackMessageEl.ValueKind == JsonValueKind.String) {
            var fallback = fallbackMessageEl.GetString();
            if (fallback != null) {
                var trimmed = fallback.Trim();
                if (trimmed.Length > 0) {
                    return trimmed;
                }
            }
        }

        return null;
    }

    /// <summary>Create subscription request payload.</summary>
    public sealed class GraphCreateSubscriptionRequest {
        /// <summary>
         /// Resource to subscribe to (for example, <c>me/mailFolders('inbox')/messages</c>).
         /// </summary>
        [JsonPropertyName("resource")]
        public string Resource { get; set; } = string.Empty;

        /// <summary>
        /// Change types (comma-separated) to subscribe to (for example, <c>created,updated,deleted</c>).
        /// </summary>
        [JsonPropertyName("changeType")]
        public string ChangeType { get; set; } = string.Empty;

        /// <summary>Webhook URL to receive notifications.</summary>
        [JsonPropertyName("notificationUrl")]
        public string NotificationUrl { get; set; } = string.Empty;

        /// <summary>Subscription expiration time.</summary>
        [JsonPropertyName("expirationDateTime")]
        public DateTimeOffset ExpirationDateTime { get; set; }

        /// <summary>Optional opaque state returned in notifications.</summary>
        [JsonPropertyName("clientState")]
        public string? ClientState { get; set; }
    }

    /// <summary>Renew subscription request payload.</summary>
    public sealed class GraphRenewSubscriptionRequest {
        /// <summary>Updated expiration time.</summary>
        [JsonPropertyName("expirationDateTime")]
        public DateTimeOffset ExpirationDateTime { get; set; }
    }

    /// <summary>Graph webhook subscription.</summary>
    public sealed class GraphSubscription {
        /// <summary>Subscription id.</summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>Subscribed resource.</summary>
        [JsonPropertyName("resource")]
        public string? Resource { get; set; }

        /// <summary>Subscribed change types.</summary>
        [JsonPropertyName("changeType")]
        public string? ChangeType { get; set; }

        /// <summary>Webhook URL.</summary>
        [JsonPropertyName("notificationUrl")]
        public string? NotificationUrl { get; set; }

        /// <summary>Subscription expiration time.</summary>
        [JsonPropertyName("expirationDateTime")]
        public DateTimeOffset ExpirationDateTime { get; set; }

        /// <summary>Optional opaque state returned in notifications.</summary>
        [JsonPropertyName("clientState")]
        public string? ClientState { get; set; }
    }

    /// <summary>Graph subscriptions list response.</summary>
    public sealed class GraphSubscriptionListResponse {
        /// <summary>List of subscriptions.</summary>
        [JsonPropertyName("value")]
        public List<GraphSubscription>? Value { get; set; }
    }
}
