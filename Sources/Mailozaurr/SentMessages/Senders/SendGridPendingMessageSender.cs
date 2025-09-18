using System.Net.Http.Headers;
using System.Text;

namespace Mailozaurr;

/// <summary>
/// Sends queued SendGrid messages using the REST API.
/// </summary>
public sealed class SendGridPendingMessageSender : IPendingMessageSender {
    internal const string MessageJsonKey = "MessageJson";
    internal const string ApiKeyKey = "ApiKey";
    internal const string ApiKeyBase64Key = "ApiKeyBase64";
    internal const string ApiKeyProtectedKey = "ApiKeyProtected";
    internal const string EndpointKey = "Endpoint";

    private static readonly Uri DefaultEndpoint = new("https://api.sendgrid.com/v3/mail/send");

    private readonly HttpClient httpClient;
    private readonly Uri endpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendGridPendingMessageSender"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client used to send requests.</param>
    /// <param name="endpoint">Optional override for the SendGrid endpoint.</param>
    public SendGridPendingMessageSender(HttpClient? httpClient = null, Uri? endpoint = null) {
        this.httpClient = httpClient ?? Helpers.SharedHttpClient;
        this.endpoint = endpoint ?? DefaultEndpoint;
    }

    /// <inheritdoc />
    public async Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }
        if (!record.ProviderData.TryGetValue(MessageJsonKey, out var json) || string.IsNullOrWhiteSpace(json)) {
            throw new InvalidOperationException("Pending SendGrid message is missing serialized payload.");
        }
        var apiKey = ResolveApiKey(record.ProviderData);
        using var request = new HttpRequestMessage(HttpMethod.Post, ResolveEndpoint(record.ProviderData));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
#if NET5_0_OR_GREATER
            var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
#else
            var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            throw new HttpRequestException($"SendGrid returned {(int)response.StatusCode} ({response.StatusCode}): {error}");
        }
    }

    private static Uri ResolveEndpoint(Dictionary<string, string> providerData) {
        if (providerData.TryGetValue(EndpointKey, out var value) && !string.IsNullOrWhiteSpace(value) && Uri.TryCreate(value, UriKind.Absolute, out var uri)) {
            return uri;
        }
        return DefaultEndpoint;
    }

    private static string ResolveApiKey(Dictionary<string, string> providerData) {
        if (providerData.TryGetValue(ApiKeyProtectedKey, out var protectedValue) && !string.IsNullOrWhiteSpace(protectedValue)) {
            var decrypted = CredentialProtection.UnprotectWithFallback(protectedValue);
            if (!string.IsNullOrEmpty(decrypted)) {
                return decrypted;
            }
        }

        if (providerData.TryGetValue(ApiKeyBase64Key, out var encoded) && !string.IsNullOrWhiteSpace(encoded)) {
            var bytes = Convert.FromBase64String(encoded);
            return Encoding.UTF8.GetString(bytes);
        }
        if (providerData.TryGetValue(ApiKeyKey, out var value) && !string.IsNullOrWhiteSpace(value)) {
            return value;
        }
        throw new InvalidOperationException("Pending SendGrid message is missing API key information.");
    }
}
