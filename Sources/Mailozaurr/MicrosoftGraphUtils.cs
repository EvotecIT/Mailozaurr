using MimeKit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr {

    /// <summary>
    /// Utility helpers for working with the Microsoft Graph API.
    /// </summary>
    public static partial class MicrosoftGraphUtils {
        private static readonly HttpClient HttpClient;
        private static readonly ConcurrentDictionary<string, GraphAuthorization> TokenCache = new();
        internal static Func<string, string, string, string, IEnumerable<string>?, Task<GraphAuthorization>> AcquireGraphCertificateTokenAsyncFunc { get; set; } = AcquireGraphCertificateTokenAsyncDefault;
        internal static Func<string, string, byte[], string, IEnumerable<string>?, Task<GraphAuthorization>> AcquireGraphCertificateBytesTokenAsyncFunc { get; set; } = AcquireGraphCertificateBytesTokenAsyncDefault;
        internal static Func<string, string, string, IEnumerable<string>?, Task<GraphAuthorization>> AcquireGraphCertificatePemTokenAsyncFunc { get; set; } = AcquireGraphCertificatePemTokenAsyncDefault;
        private static SemaphoreSlim _concurrencySemaphore = new(5, 5);
        private static int _maxConcurrentRequests = 5;

        /// <summary>
        /// Gets or sets the maximum number of concurrent HTTP requests allowed.
        /// </summary>
        /// <remarks>
        /// This is a process-wide limit. Setting this property affects all Graph operations in the
        /// current AppDomain. The underlying semaphore is swapped using a thread-safe exchange to
        /// ensure safe updates under concurrency.
        /// </remarks>
        public static int MaxConcurrentRequests {
            get => _maxConcurrentRequests;
            set {
                if (value <= 0) {
                    throw new ArgumentOutOfRangeException(nameof(MaxConcurrentRequests));
                }
                var newSem = new SemaphoreSlim(value, value);
                var old = Interlocked.Exchange(ref _concurrencySemaphore, newSem);
                old.Dispose();
                _maxConcurrentRequests = value;
            }
        }

        internal static SemaphoreSlim ConcurrencySemaphore => _concurrencySemaphore;

        internal static string GetEndpointBase(GraphEndpoint endpoint) =>
            endpoint switch {
                GraphEndpoint.V1 => "https://graph.microsoft.com/v1.0",
                GraphEndpoint.Beta => "https://graph.microsoft.com/beta",
                _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
            };

        private static TimeSpan GetRetryAfterDelay(HttpResponseMessage response) {
            if (response.Headers.TryGetValues("Retry-After", out var values)) {
                var value = System.Linq.Enumerable.FirstOrDefault(values);
                if (int.TryParse(value, out var seconds)) {
                    return TimeSpan.FromSeconds(seconds);
                }
                if (DateTimeOffset.TryParse(value, out var date)) {
                    var diff = date - DateTimeOffset.UtcNow;
                    return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
                }
            }
            return TimeSpan.Zero;
        }

        /// <summary>
        /// Gets or sets the timeout for HTTP operations in seconds.
        /// </summary>
        public static int TimeoutSeconds {
            get => (int)HttpClient.Timeout.TotalSeconds;
            set => HttpClient.Timeout = TimeSpan.FromSeconds(value);
        }

        static MicrosoftGraphUtils() {
            HttpClient = new HttpClient();
            HttpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => HttpClient.Dispose();
        }

        internal static void ResetOAuthHelperOverrides() {
            AcquireGraphCertificateTokenAsyncFunc = AcquireGraphCertificateTokenAsyncDefault;
            AcquireGraphCertificateBytesTokenAsyncFunc = AcquireGraphCertificateBytesTokenAsyncDefault;
            AcquireGraphCertificatePemTokenAsyncFunc = AcquireGraphCertificatePemTokenAsyncDefault;
        }

        private static Task<GraphAuthorization> AcquireGraphCertificateTokenAsyncDefault(string clientId, string tenantDomain, string certificatePath, string certificatePassword, IEnumerable<string>? scopes) =>
            OAuthHelpers.AcquireGraphCertificateTokenAsync(clientId, tenantDomain, certificatePath, certificatePassword, scopes);

        private static Task<GraphAuthorization> AcquireGraphCertificateBytesTokenAsyncDefault(string clientId, string tenantDomain, byte[] certificateBytes, string certificatePassword, IEnumerable<string>? scopes) =>
            OAuthHelpers.AcquireGraphCertificateTokenAsync(clientId, tenantDomain, certificateBytes, certificatePassword, scopes);

        private static Task<GraphAuthorization> AcquireGraphCertificatePemTokenAsyncDefault(string clientId, string tenantDomain, string pemPath, IEnumerable<string>? scopes) =>
            OAuthHelpers.AcquireGraphCertificatePemTokenAsync(clientId, tenantDomain, pemPath, scopes);

        private static async Task CacheGraphAuthorizationAsync(string key, string clientId, GraphAuthorization authorization) {
            TokenCache[key] = authorization;
            await OAuthTokenCache.SetAsync($"graph:{key}", new OAuthCredential {
                UserName = clientId,
                AccessToken = authorization.AccessToken,
                ExpiresOn = authorization.ExpiresOn
            }).ConfigureAwait(false);
        }

        internal static string BuildGraphTokenCacheKey(
            GraphCredential credential,
            string tenantDomain,
            string resource) {
            if (credential == null) {
                throw new ArgumentNullException(nameof(credential));
            }

            string certificateBytesHash = credential.CertificateBytes == null
                ? string.Empty
                : ComputeSha256Hex(credential.CertificateBytes);
            string identity = string.Join(
                "\n",
                credential.ClientId ?? string.Empty,
                tenantDomain ?? string.Empty,
                resource ?? string.Empty,
                credential.CertificatePath ?? string.Empty,
                credential.CertificatePemPath ?? string.Empty,
                certificateBytesHash,
                credential.ClientSecret ?? string.Empty);

            return $"v2:{ComputeSha256Hex(Encoding.UTF8.GetBytes(identity))}";
        }

        private static string ComputeSha256Hex(byte[] value) {
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(value);
            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte item in hash) {
                builder.Append(item.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }
        /// <summary>
        /// Converts a credential string (username@directory) and secret to a GraphCredential object.
        /// </summary>
        public static GraphCredential ConvertFromGraphCredential(string username, string password) {
            if (username == null) {
                throw new ArgumentNullException(nameof(username));
            }

            if (string.IsNullOrWhiteSpace(username)) {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(username));
            }

            if (password == null) {
                throw new ArgumentNullException(nameof(password));
            }

            if (string.IsNullOrWhiteSpace(password)) {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(password));
            }

            username = username.Trim();
            var parts = username.Split('@');
            if (parts.Length != 2) {
                throw new ArgumentException("Invalid credential format. Expected 'clientid@directoryid'.");
            }

            return new GraphCredential {
                ClientId = parts[0],
                DirectoryId = parts[1],
                ClientSecret = password
            };
        }

        /// <summary>
        /// Connects to O365 Graph and returns the Authorization header value ("Bearer ...").
        /// </summary>
        public static async Task<string> ConnectO365GraphAsync(GraphCredential credential, string tenantDomain, string resource = "https://manage.office.com", CancellationToken cancellationToken = default) {
            var delegatedAccessToken = credential.AccessToken;
            if (delegatedAccessToken != null && delegatedAccessToken.Trim().Length > 0) {
                var accessToken = delegatedAccessToken.Trim();
                return accessToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? accessToken
                    : $"Bearer {accessToken}";
            }

            string key = BuildGraphTokenCacheKey(credential, tenantDomain, resource);
            if (TokenCache.TryGetValue(key, out var cached) && cached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5)) {
                return $"{cached.TokenType} {cached.AccessToken}";
            }
            var cachedFile = await OAuthTokenCache.GetAsync($"graph:{key}").ConfigureAwait(false);
            if (cachedFile != null && cachedFile.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5)) {
                TokenCache[key] = new GraphAuthorization { AccessToken = cachedFile.AccessToken, TokenType = "Bearer", ExpiresOn = cachedFile.ExpiresOn };
                return $"Bearer {cachedFile.AccessToken}";
            }
            if (!string.IsNullOrWhiteSpace(credential.CertificatePath)) {
                var scopes = new[] { $"{resource}/.default" };
                var auth = await AcquireGraphCertificateTokenAsyncFunc(
                    credential.ClientId,
                    tenantDomain,
                    credential.CertificatePath!,
                    credential.CertificatePassword ?? string.Empty,
                    scopes).ConfigureAwait(false);
                await CacheGraphAuthorizationAsync(key, credential.ClientId, auth).ConfigureAwait(false);
                return $"{auth.TokenType} {auth.AccessToken}";
            }
            if (credential.CertificateBytes != null) {
                var scopes = new[] { $"{resource}/.default" };
                var auth = await AcquireGraphCertificateBytesTokenAsyncFunc(
                    credential.ClientId,
                    tenantDomain,
                    credential.CertificateBytes,
                    credential.CertificatePassword ?? string.Empty,
                    scopes).ConfigureAwait(false);
                await CacheGraphAuthorizationAsync(key, credential.ClientId, auth).ConfigureAwait(false);
                return $"{auth.TokenType} {auth.AccessToken}";
            }
            if (!string.IsNullOrWhiteSpace(credential.CertificatePemPath)) {
                var scopes = new[] { $"{resource}/.default" };
                var auth = await AcquireGraphCertificatePemTokenAsyncFunc(
                    credential.ClientId,
                    tenantDomain,
                    credential.CertificatePemPath!,
                    scopes).ConfigureAwait(false);
                await CacheGraphAuthorizationAsync(key, credential.ClientId, auth).ConfigureAwait(false);
                return $"{auth.TokenType} {auth.AccessToken}";
            }

            var body = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "resource", resource },
                { "client_id", credential.ClientId }
            };
            if (!string.IsNullOrEmpty(credential.ClientSecret)) {
                body.Add("client_secret", credential.ClientSecret!);
            }
            var content = new FormUrlEncodedContent(body);
            var url = $"https://login.microsoftonline.com/{tenantDomain}/oauth2/token";
            await ConcurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            HttpResponseMessage? response = null;
            try {
                response = await HttpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
                if ((int)response.StatusCode == 429) {
                    var delay = GetRetryAfterDelay(response);
                    response.Dispose();
                    if (delay > TimeSpan.Zero) {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    response = await HttpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
                }
#if NET5_0_OR_GREATER
                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
                if (!response.IsSuccessStatusCode) {
                    throw new GraphApiException(
                        response.StatusCode,
                        $"ConnectO365GraphAsync - Error: {json}",
                        json);
                }
                var token = System.Text.Json.JsonDocument.Parse(json);
                var accessToken = token.RootElement.GetProperty("access_token").GetString();
                var tokenType = token.RootElement.GetProperty("token_type").GetString();
                var expiresOn = DateTimeOffset.UtcNow.AddHours(1);
                if (token.RootElement.TryGetProperty("expires_in", out var expIn)) {
                    if (expIn.ValueKind == System.Text.Json.JsonValueKind.Number && expIn.TryGetInt32(out var expiresInSeconds)) {
                        expiresOn = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
                    } else if (expIn.ValueKind == System.Text.Json.JsonValueKind.String &&
                               int.TryParse(expIn.GetString(), out var expiresInStringSeconds)) {
                        expiresOn = DateTimeOffset.UtcNow.AddSeconds(expiresInStringSeconds);
                    }
                }
                if (token.RootElement.TryGetProperty("expires_on", out var expOn)) {
                    if (long.TryParse(expOn.GetString(), out var expSeconds)) {
                        expiresOn = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
                    }
                }
                TokenCache[key] = new GraphAuthorization { AccessToken = accessToken ?? string.Empty, TokenType = tokenType ?? string.Empty, ExpiresOn = expiresOn };
                await OAuthTokenCache.SetAsync($"graph:{key}", new OAuthCredential {
                    UserName = credential.ClientId,
                    AccessToken = accessToken ?? string.Empty,
                    ExpiresOn = expiresOn
                }).ConfigureAwait(false);
                return $"{tokenType} {accessToken}";
            } finally {
                response?.Dispose();
                ConcurrencySemaphore.Release();
            }
        }

        /// <summary>
        /// Connects to O365 Graph with retry logic and returns the Authorization header value.
        /// </summary>
        public static async Task<string> ConnectO365GraphWithRetryAsync(
            GraphCredential credential,
            string tenantDomain,
            int retryCount,
            int retryDelayMilliseconds,
            double retryDelayBackoff,
            string resource = "https://manage.office.com",
            CancellationToken cancellationToken = default) {
            int attempts = 0;
            Exception? lastException = null;
            do {
                try {
                    return await ConnectO365GraphAsync(credential, tenantDomain, resource, cancellationToken).ConfigureAwait(false);
                } catch (Exception ex) {
                    lastException = ex;
                    LoggingMessages.Logger.WriteWarning($"Connect-EmailGraph - {ex.Message}");
                    if ((!Helpers.IsTransient(ex)) || attempts >= retryCount) {
                        throw;
                    }
                    var delay = (int)Math.Round(retryDelayMilliseconds * Math.Pow(retryDelayBackoff, attempts));
                    if (delay > 0) {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                }
                attempts++;
            } while (attempts <= retryCount);
            throw lastException ?? new InvalidOperationException("Operation failed without exception");
        }

        /// <summary>
        /// Builds a full URI from base, path, and query parameters.
        /// </summary>
        public static string BuildGraphUri(GraphEndpoint endpoint, string path, IDictionary<string, string>? queryParameters = null) =>
            BuildGraphUri(GetEndpointBase(endpoint), path, queryParameters);

        /// <summary>
        /// Builds a full URI from base, path, and query parameters.
        /// </summary>
        public static string BuildGraphUri(string baseUri, string path, IDictionary<string, string>? queryParameters = null) {
            var uriBuilder = new StringBuilder();
            uriBuilder.Append(baseUri.TrimEnd('/'));
            if (!string.IsNullOrWhiteSpace(path)) {
                if (!path.StartsWith("/")) uriBuilder.Append('/');
                uriBuilder.Append(path);
            }
            if (queryParameters != null && queryParameters.Count > 0) {
                var first = true;
                foreach (var kvp in queryParameters) {
                    if (string.IsNullOrWhiteSpace(kvp.Value)) continue;
                    uriBuilder.Append(first ? '?' : '&');
                    uriBuilder.Append(Uri.EscapeDataString(kvp.Key));
                    uriBuilder.Append('=');
                    uriBuilder.Append(Uri.EscapeDataString(kvp.Value));
                    first = false;
                }
            }
            return uriBuilder.ToString();
        }

        /// <summary>
        /// Invokes a REST API call to Microsoft Graph and returns the result as a JsonDocument.
        /// </summary>
        public static async Task<JsonDocument> InvokeGraphApiAsync(
            string method,
            string uri,
            IDictionary<string, string>? headers = null,
            string? body = null,
            CancellationToken cancellationToken = default) {
            var request = new HttpRequestMessage(new HttpMethod(method), uri);
            if (headers != null) {
                foreach (var kvp in headers) {
                    request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                }
            }
            if (!string.IsNullOrWhiteSpace(body) && (method == "POST" || method == "PUT" || method == "PATCH")) {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }
            await ConcurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            HttpResponseMessage? response = null;
            try {
                response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if ((int)response.StatusCode == 429) {
                    var delay = GetRetryAfterDelay(response);
                    response.Dispose();
                    if (delay > TimeSpan.Zero) {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
#if NET5_0_OR_GREATER
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
                if (!response.IsSuccessStatusCode) {
                    throw new GraphApiException(
                        response.StatusCode,
                        $"InvokeGraphApiAsync - Error: {response.StatusCode} - {responseContent}",
                        responseContent);
                }
                return JsonDocument.Parse(responseContent);
            } finally {
                response?.Dispose();
                ConcurrencySemaphore.Release();
            }
        }

        /// <summary>
        /// Sends multiple requests to Microsoft Graph in a single batch.
        /// </summary>
        public static async Task<IReadOnlyList<GraphBatchResult>> SendBatchAsync(GraphCredential credential, IEnumerable<GraphBatchRequest> requests, CancellationToken cancellationToken = default) {
            var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com", cancellationToken).ConfigureAwait(false);
            var headers = new Dictionary<string, string> { { "Authorization", token } };
            var batchPayload = new GraphBatchPayload {
                Requests = requests.Select(r => new GraphBatchRequestPayload {
                    Id = r.Id,
                    Method = r.Method.ToString(),
                    Url = r.Url.TrimStart('/'),
                    Headers = r.Headers,
                    Body = r.Body
                }).ToList()
            };
            var jsonBody = JsonSerializer.Serialize(batchPayload, GraphJsonContext.Default.GraphBatchPayload);
            var batchUri = BuildGraphUri(GraphEndpoint.V1, "/$batch");
            var doc = await InvokeGraphApiAsync("POST", batchUri, headers, jsonBody, cancellationToken).ConfigureAwait(false);
            var results = new List<GraphBatchResult>();
            if (doc.RootElement.TryGetProperty("responses", out var responses) && responses.ValueKind == JsonValueKind.Array) {
                foreach (var item in responses.EnumerateArray()) {
                    var result = new GraphBatchResult();
                    if (item.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String) result.Id = idEl.GetString() ?? string.Empty;
                    if (item.TryGetProperty("status", out var statusEl) && statusEl.TryGetInt32(out var status)) result.Status = status;
                    if (item.TryGetProperty("headers", out var headersEl) && headersEl.ValueKind == JsonValueKind.Object) {
                        var h = new Dictionary<string, string>();
                        foreach (var prop in headersEl.EnumerateObject()) {
                            if (prop.Value.ValueKind == JsonValueKind.String) h[prop.Name] = prop.Value.GetString() ?? string.Empty;
                        }
                        result.Headers = h;
                    }
                    if (item.TryGetProperty("body", out var bodyEl)) {
                        result.Body = bodyEl;
                    }
                    results.Add(result);
                }
            }
            return results;
        }

        /// <summary>
        /// Removes empty values from a dictionary recursively (null, empty string, empty array, empty dictionary).
        /// </summary>
        public static void RemoveEmptyValues(IDictionary<string, object> dict, HashSet<string>? exclude = null, bool recursive = true, int rerun = 0) {
            exclude ??= new HashSet<string>();
            var keys = dict.Keys.ToList();
            foreach (var key in keys) {
                if (exclude.Contains(key)) continue;
                var value = dict[key];
                if (recursive && value is IDictionary<string, object> subDict) {
                    if (subDict.Count == 0) {
                        dict.Remove(key);
                    } else {
                        RemoveEmptyValues(subDict, exclude, recursive, rerun);
                        if (subDict.Count == 0) dict.Remove(key);
                    }
                } else if (value == null) {
                    dict.Remove(key);
                } else if (value is string s && string.IsNullOrWhiteSpace(s)) {
                    dict.Remove(key);
                } else if (value is System.Collections.IList list && list.Count == 0) {
                    dict.Remove(key);
                } else if (value is IDictionary<string, object> d && d.Count == 0) {
                    dict.Remove(key);
                }
            }
            if (rerun > 0) {
                for (int i = 0; i < rerun; i++) {
                    RemoveEmptyValues(dict, exclude, recursive, 0);
                }
            }
        }

        /// <summary>
        /// Joins a base URI, optional relative URI, and query parameters into a full URI string.
        /// </summary>
        public static string JoinUriQuery(GraphEndpoint endpoint, string? relativeOrAbsoluteUri = null, IDictionary<string, object>? queryParameters = null, bool escapeUriString = false) =>
            JoinUriQuery(GetEndpointBase(endpoint), relativeOrAbsoluteUri, queryParameters, escapeUriString);

        /// <summary>
        /// Joins a base URI, optional relative URI, and query parameters into a full URI string.
        /// </summary>
        public static string JoinUriQuery(string baseUri, string? relativeOrAbsoluteUri = null, IDictionary<string, object>? queryParameters = null, bool escapeUriString = false) {
            if (baseUri == null) {
                throw new ArgumentNullException(nameof(baseUri));
            }
            string url = baseUri.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(relativeOrAbsoluteUri)) {
                url += "/" + relativeOrAbsoluteUri!.TrimStart('/');
            }
            var uriBuilder = new UriBuilder(url);
            if (queryParameters != null && queryParameters.Count > 0) {
                var query = new StringBuilder();
                bool first = true;
                foreach (var kvp in queryParameters) {
                    if (kvp.Value == null) {
                        continue;
                    }

                    if (!first) {
                        query.Append('&');
                    }

                    query.Append(Uri.EscapeDataString(kvp.Key));
                    query.Append('=');
                    var valueString = kvp.Value.ToString() ?? string.Empty;
                    query.Append(Uri.EscapeDataString(valueString));
                    first = false;
                }
                uriBuilder.Query = query.ToString();
            }
            var result = uriBuilder.Uri.AbsoluteUri;
            if (escapeUriString) {
                if (Uri.TryCreate(result, UriKind.Absolute, out var uri)) {
                    result = uri.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
                } else {
                    result = Uri.EscapeDataString(result);
                }
            }
            return result;
        }

        private static object? ConvertJsonElementToNativeObject(JsonElement element) {
            switch (element.ValueKind) {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var prop in element.EnumerateObject())
                        dict[prop.Name] = ConvertJsonElementToNativeObject(prop.Value)!;
                    return dict;
                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                        list.Add(ConvertJsonElementToNativeObject(item)!);
                    return list.ToArray();
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var l)) return l;
                    if (element.TryGetDouble(out var d)) return d;
                    return element.GetRawText();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();
                case JsonValueKind.Null:
                default:
                    return null;
            }
        }


    }
}
