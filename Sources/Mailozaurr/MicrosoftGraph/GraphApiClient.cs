using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Initializes the client using the provided OAuth credential.
    /// </summary>
    /// <param name="credential">OAuth credential holding an access token.</param>
    /// <param name="refreshToken">Optional delegate used to refresh an access token when a request returns 401/403.</param>
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
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
    }

    internal GraphApiClient(HttpClient client, Func<CancellationToken, Task<string>>? refreshToken = null, OAuthCredential? credential = null) {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _refreshToken = refreshToken;
        _credential = credential;
        if (credential != null && !string.IsNullOrEmpty(credential.AccessToken) && _client.DefaultRequestHeaders.Authorization == null) {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
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
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
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

        var json = JsonSerializer.Serialize(request, MailozaurrJsonContext.Default.GraphCreateSubscriptionRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync("subscriptions", content, cancellationToken).ConfigureAwait(false);
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

        var request = new GraphRenewSubscriptionRequest { ExpirationDateTime = expirationDateTime };
        var json = JsonSerializer.Serialize(request, MailozaurrJsonContext.Default.GraphRenewSubscriptionRequest);
        var requestUri = _client.BaseAddress != null
            ? new Uri(_client.BaseAddress, $"subscriptions/{subscriptionId}")
            : new Uri($"subscriptions/{subscriptionId}", UriKind.Relative);
        using var msg = new HttpRequestMessage(new HttpMethod("PATCH"), requestUri) {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        using var response = await _client.SendAsync(msg, cancellationToken).ConfigureAwait(false);
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
        using var response = await _client.DeleteAsync($"subscriptions/{subscriptionId}", cancellationToken).ConfigureAwait(false);
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
        using var response = await _client.GetAsync("subscriptions", cancellationToken).ConfigureAwait(false);
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
