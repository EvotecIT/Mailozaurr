using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mailozaurr;

public sealed partial class JmapApiClient {
    private async Task<TResponse> CallAsync<TArguments, TResponse>(
        JmapSessionResource session,
        string methodName,
        TArguments arguments,
        JsonTypeInfo<TArguments> argumentsType,
        JsonTypeInfo<TResponse> responseType,
        CancellationToken cancellationToken) {
        var apiUrl = ResolveApiUrl(session);
        var callId = "c" + Interlocked.Increment(ref _callSequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
        byte[] requestBody;
        using (var buffer = new MemoryStream()) {
            using (var writer = new Utf8JsonWriter(buffer)) {
                writer.WriteStartObject();
                writer.WritePropertyName("using");
                writer.WriteStartArray();
                writer.WriteStringValue(JmapCapabilities.Core);
                writer.WriteStringValue(JmapCapabilities.Mail);
                if (methodName.StartsWith("Identity/", StringComparison.Ordinal)) writer.WriteStringValue(JmapCapabilities.Submission);
                writer.WriteEndArray();
                writer.WritePropertyName("methodCalls");
                writer.WriteStartArray();
                writer.WriteStartArray();
                writer.WriteStringValue(methodName);
                writer.WriteRawValue(JsonSerializer.Serialize(arguments, argumentsType));
                writer.WriteStringValue(callId);
                writer.WriteEndArray();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            requestBody = buffer.ToArray();
        }

        using var request = CreateRequest(HttpMethod.Post, apiUrl);
        request.Content = new ByteArrayContent(requestBody);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
            throw new JmapApiException("requestFailed", $"JMAP method request failed ({(int)response.StatusCode}).");
        }
        var payload = await ReadBoundedAsync(response.Content, cancellationToken).ConfigureAwait(false);
        try {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("methodResponses", out var responses) || responses.ValueKind != JsonValueKind.Array) {
                throw new JmapApiException("invalidResponse", "JMAP response did not contain methodResponses.");
            }
            var tuples = responses.EnumerateArray().ToArray();
            if (tuples.Length != 1 || tuples[0].ValueKind != JsonValueKind.Array) {
                throw new JmapApiException("invalidResponse", "JMAP returned an unexpected method response count.");
            }
            var tuple = tuples[0].EnumerateArray().ToArray();
            if (tuple.Length != 3 || tuple[0].ValueKind != JsonValueKind.String || tuple[2].ValueKind != JsonValueKind.String) {
                throw new JmapApiException("invalidResponse", "JMAP returned an invalid method response tuple.");
            }
            var returnedMethod = tuple[0].GetString();
            var returnedCallId = tuple[2].GetString();
            if (!string.Equals(returnedCallId, callId, StringComparison.Ordinal)) {
                throw new JmapApiException("invalidResponse", "JMAP returned a response for a different call identifier.");
            }
            if (string.Equals(returnedMethod, "error", StringComparison.Ordinal)) {
                var errorType = tuple[1].ValueKind == JsonValueKind.Object && tuple[1].TryGetProperty("type", out var type)
                    ? type.GetString()
                    : null;
                throw new JmapApiException(errorType ?? "methodError", $"JMAP method '{methodName}' failed with '{errorType ?? "methodError"}'.");
            }
            if (!string.Equals(returnedMethod, methodName, StringComparison.Ordinal)) {
                throw new JmapApiException("invalidResponse", "JMAP returned a response for a different method.");
            }
            return JsonSerializer.Deserialize(tuple[1].GetRawText(), responseType)
                ?? throw new JmapApiException("invalidResponse", $"JMAP method '{methodName}' returned an empty result.");
        } catch (JsonException) {
            throw new JmapApiException("invalidResponse", "JMAP returned invalid JSON.");
        }
    }

    private async Task<(JmapSessionResource Session, string AccountId)> ResolveAccountAsync(
        string? accountId,
        string capability,
        CancellationToken cancellationToken) {
        var session = await GetSessionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        string resolved;
        if (!string.IsNullOrWhiteSpace(accountId)) {
            resolved = accountId!.Trim();
        } else if (!session.PrimaryAccounts.TryGetValue(capability, out resolved!) || string.IsNullOrWhiteSpace(resolved)) {
            throw new JmapApiException("accountNotFound", $"The JMAP Session resource did not identify a primary account for '{capability}'.");
        }
        if (!session.Accounts.TryGetValue(resolved, out var account)) {
            throw new JmapApiException("accountNotFound", "The selected JMAP account was not present in the Session resource.");
        }
        if (!account.AccountCapabilities.ContainsKey(capability)) {
            throw new JmapApiException("accountCapabilityMissing", $"The selected JMAP account does not advertise '{capability}'.");
        }
        return (session, resolved);
    }

    private Uri ResolveApiUrl(JmapSessionResource session) {
        if (!Uri.TryCreate(session.ApiUrl, UriKind.Absolute, out var apiUrl)) {
            throw new JmapApiException("invalidSession", "The JMAP Session resource did not contain an absolute API URL.");
        }
        apiUrl = ValidateHttpsUri(apiUrl, "API URL");
        if (!_allowCrossOriginApiUrl && !HasSameOrigin(SessionUrl, apiUrl)) {
            throw new JmapApiException("crossOriginApiUrl", "The JMAP API URL uses a different origin; explicit cross-origin authorization is required.");
        }
        return apiUrl;
    }

    private void ValidateSession(JmapSessionResource? session) {
        if (session == null) throw new JmapApiException("invalidSession", "JMAP returned an empty Session resource.");
        if (!session.Capabilities.ContainsKey(JmapCapabilities.Core)) {
            throw new JmapApiException("capabilityMissing", "The server does not advertise the JMAP core capability.");
        }
        if (!session.Capabilities.ContainsKey(JmapCapabilities.Mail)) {
            throw new JmapApiException("capabilityMissing", "The server does not advertise JMAP mail.");
        }
        _ = ResolveApiUrl(session);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri) {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken) {
        var maximum = Clamp(MaximumResponseBytes, 1024, 128 * 1024 * 1024);
#if NET5_0_OR_GREATER
        using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        using var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true) {
            var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (output.Length + read > maximum) throw new JmapApiException("responseTooLarge", "JMAP response exceeded the configured byte limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static Uri ValidateHttpsUri(Uri uri, string label) {
        if (!uri.IsAbsoluteUri || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment)) {
            throw new ArgumentException($"JMAP {label} must be an absolute HTTPS URL without user information or a fragment.", nameof(uri));
        }
        return uri;
    }

    private static bool HasSameOrigin(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) &&
        left.Port == right.Port;

    private static string[] NormalizeIds(IReadOnlyCollection<string> ids, string parameterName) {
        if (ids == null) throw new ArgumentNullException(parameterName);
        var normalized = ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(5001)
            .ToArray();
        if (normalized.Length == 0) throw new ArgumentException("At least one identifier is required.", parameterName);
        if (normalized.Length > 5000) throw new ArgumentOutOfRangeException(parameterName, "No more than 5000 identifiers may be requested.");
        return normalized;
    }

    private static string[]? NormalizeProperties(IReadOnlyCollection<string>? properties) {
        if (properties == null) return null;
        var normalized = properties.Where(property => !string.IsNullOrWhiteSpace(property))
            .Select(property => property.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(256)
            .ToArray();
        return normalized.Length == 0 ? null : normalized;
    }

    private static int Clamp(int value, int minimum, int maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;

    private void ThrowIfDisposed() {
        if (_disposed) throw new ObjectDisposedException(nameof(JmapApiClient));
    }
}
