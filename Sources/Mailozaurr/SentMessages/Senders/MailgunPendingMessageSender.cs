using System.Net.Http.Headers;
using System.Text;

namespace Mailozaurr;

/// <summary>
/// Sends queued Mailgun messages using the REST API.
/// </summary>
public sealed class MailgunPendingMessageSender : IPendingMessageSender {
    internal const string DomainKey = "Domain";
    internal const string ApiKeyKey = "ApiKey";
    internal const string ApiKeyBase64Key = "ApiKeyBase64";
    internal const string ApiKeyProtectedKey = "ApiKeyProtected";
    internal const string EndpointKey = "Endpoint";

    private readonly HttpClient httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailgunPendingMessageSender"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client used to send requests.</param>
    public MailgunPendingMessageSender(HttpClient? httpClient = null) {
        this.httpClient = httpClient ?? Helpers.SharedHttpClient;
    }

    /// <inheritdoc />
    public async Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }
        if (string.IsNullOrWhiteSpace(record.MimeMessage)) {
            throw new InvalidOperationException("Pending Mailgun message does not contain MIME content.");
        }
        if (!record.ProviderData.TryGetValue(DomainKey, out var domain) || string.IsNullOrWhiteSpace(domain)) {
            throw new InvalidOperationException("Pending Mailgun message is missing the Mailgun domain.");
        }
        var apiKey = ResolveApiKey(record.ProviderData);
        var endpoint = ResolveEndpoint(record.ProviderData, domain);
        var bytes = Convert.FromBase64String(record.MimeMessage);

        using var content = new MultipartFormDataContent();
        using var messageContent = new ByteArrayContent(bytes);
        messageContent.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
        var fileName = string.IsNullOrWhiteSpace(record.MessageId) ? "message.eml" : $"{record.MessageId}.eml";
        content.Add(messageContent, "message", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
            Content = content
        };
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{apiKey}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
#if NET5_0_OR_GREATER
            var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
#else
            var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            throw new HttpRequestException($"Mailgun returned {(int)response.StatusCode} ({response.StatusCode}): {error}");
        }
    }

    private static Uri ResolveEndpoint(Dictionary<string, string> providerData, string domain) {
        if (providerData.TryGetValue(EndpointKey, out var value) && !string.IsNullOrWhiteSpace(value) && Uri.TryCreate(value, UriKind.Absolute, out var uri)) {
            return uri;
        }
        return new Uri($"https://api.mailgun.net/v3/{domain}/messages.mime");
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
        throw new InvalidOperationException("Pending Mailgun message is missing API key information.");
    }
}
