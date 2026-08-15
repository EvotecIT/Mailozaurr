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

public sealed partial class GraphApiClient {
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

        var json = JsonSerializer.Serialize(request, GraphJsonContext.Default.GraphCreateSubscriptionRequest);
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
            result = JsonSerializer.Deserialize(body, GraphJsonContext.Default.GraphSubscription);
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
        var json = JsonSerializer.Serialize(request, GraphJsonContext.Default.GraphRenewSubscriptionRequest);
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
            result = JsonSerializer.Deserialize(body, GraphJsonContext.Default.GraphSubscription);
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
            result = JsonSerializer.Deserialize(body, GraphJsonContext.Default.GraphSubscriptionListResponse);
        } catch (JsonException ex) {
            throw new InvalidDataException("Failed to parse Graph subscriptions list response.", ex);
        }
        return (IReadOnlyList<GraphSubscription>?)result?.Value ?? Array.Empty<GraphSubscription>();
    }
}