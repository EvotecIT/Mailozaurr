using System.Text;

namespace Mailozaurr;

/// <summary>
/// Sends queued Amazon SES messages using the REST API.
/// </summary>
public sealed class SesPendingMessageSender : IPendingMessageSender {
    internal const string AccessKeyIdKey = "AccessKeyId";
    internal const string SecretAccessKeyKey = "SecretAccessKey";
    internal const string AccessKeyIdBase64Key = "AccessKeyIdBase64";
    internal const string SecretAccessKeyBase64Key = "SecretAccessKeyBase64";
    internal const string AccessKeyIdProtectedKey = "AccessKeyIdProtected";
    internal const string SecretAccessKeyProtectedKey = "SecretAccessKeyProtected";
    internal const string RegionKey = "Region";

    private readonly HttpClient httpClient;
    private readonly Func<DateTime> utcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="SesPendingMessageSender"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client used to send requests.</param>
    /// <param name="utcNow">Clock used for signing requests.</param>
    public SesPendingMessageSender(HttpClient? httpClient = null, Func<DateTime>? utcNow = null) {
        this.httpClient = httpClient ?? Helpers.SharedHttpClient;
        this.utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <inheritdoc />
    public async Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }
        if (string.IsNullOrWhiteSpace(record.MimeMessage)) {
            throw new InvalidOperationException("Pending SES message does not contain MIME content.");
        }
        var accessKey = ResolveAccessKey(record.ProviderData);
        var secretKey = ResolveSecretKey(record.ProviderData);
        var region = record.ProviderData.TryGetValue(RegionKey, out var regionValue) && !string.IsNullOrWhiteSpace(regionValue)
            ? regionValue
            : "us-east-1";

        var body = BuildRequestBody(record.MimeMessage);
        var request = SesRequestFactory.Create(accessKey, secretKey, region, body, utcNow());

        using (request)
        using (var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false)) {
            if (!response.IsSuccessStatusCode) {
#if NET5_0_OR_GREATER
                var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
#else
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
                throw HttpRetryPolicy.CreateFailure(
                    response.StatusCode,
                    $"SES returned {(int)response.StatusCode} ({response.StatusCode}): {error}");
            }
        }
    }

    private static string BuildRequestBody(string mimeMessageBase64) {
        return $"Action=SendRawEmail&RawMessage.Data={Uri.EscapeDataString(mimeMessageBase64)}&Version=2010-12-01";
    }

    private static string ResolveAccessKey(Dictionary<string, string> providerData) {
        if (providerData.TryGetValue(AccessKeyIdProtectedKey, out var protectedValue) && !string.IsNullOrWhiteSpace(protectedValue)) {
            var decrypted = CredentialProtection.UnprotectWithFallback(protectedValue);
            if (!string.IsNullOrEmpty(decrypted)) {
                return decrypted;
            }
        }

        if (providerData.TryGetValue(AccessKeyIdBase64Key, out var encoded) && !string.IsNullOrWhiteSpace(encoded)) {
            var bytes = Convert.FromBase64String(encoded);
            return Encoding.UTF8.GetString(bytes);
        }

        if (providerData.TryGetValue(AccessKeyIdKey, out var accessKey) && !string.IsNullOrWhiteSpace(accessKey)) {
            return accessKey;
        }

        throw new InvalidOperationException("Pending SES message is missing the AWS access key identifier.");
    }

    private static string ResolveSecretKey(Dictionary<string, string> providerData) {
        if (providerData.TryGetValue(SecretAccessKeyProtectedKey, out var protectedValue) && !string.IsNullOrWhiteSpace(protectedValue)) {
            var decrypted = CredentialProtection.UnprotectWithFallback(protectedValue);
            if (!string.IsNullOrEmpty(decrypted)) {
                return decrypted;
            }
        }

        if (providerData.TryGetValue(SecretAccessKeyBase64Key, out var encoded) && !string.IsNullOrWhiteSpace(encoded)) {
            var bytes = Convert.FromBase64String(encoded);
            return Encoding.UTF8.GetString(bytes);
        }

        if (providerData.TryGetValue(SecretAccessKeyKey, out var secretKey) && !string.IsNullOrWhiteSpace(secretKey)) {
            return secretKey;
        }

        throw new InvalidOperationException("Pending SES message is missing the AWS secret access key.");
    }

}
