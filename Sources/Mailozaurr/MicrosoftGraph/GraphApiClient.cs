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
public sealed partial class GraphApiClient : IDisposable {
    private readonly HttpClient _client;
    private readonly Func<CancellationToken, Task<string>>? _refreshToken;
    private readonly OAuthCredential? _credential;
    private readonly bool _disposeClient;
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
        _disposeClient = true;
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
    /// <param name="ownsHttpClient">When <c>true</c>, disposing this API client also disposes <paramref name="client"/>.</param>
    public GraphApiClient(
        HttpClient client,
        Func<CancellationToken, Task<string>>? refreshToken = null,
        OAuthCredential? credential = null,
        Uri? baseAddress = null,
        bool ownsHttpClient = false) {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _refreshToken = refreshToken;
        _credential = credential;
        _disposeClient = ownsHttpClient;
        if (_client.BaseAddress == null) {
            _client.BaseAddress = baseAddress ?? new Uri("https://graph.microsoft.com/v1.0/");
        }
    }

    /// <inheritdoc />
    public void Dispose() {
        if (_disposed) {
            return;
        }

        if (_disposeClient) {
            _client.Dispose();
        }

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





}