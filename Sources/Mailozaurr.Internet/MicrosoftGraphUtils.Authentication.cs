using System.Globalization;
using System.Threading;

namespace Mailozaurr;

public static partial class MicrosoftGraphUtils {
    internal const string AuthenticationFailure = "Microsoft Graph authentication failed.";

    // This primitive never consults persisted credentials and never exposes provider diagnostics.
    internal static async Task<GraphAuthorization> AcquireClientCredentialsAuthorizationAsync(
        GraphCredential credential, string tenant, string resource, HttpClient client, CancellationToken cancellationToken) {
        try {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["grant_type"] = "client_credentials",
                ["resource"] = resource,
                ["client_id"] = credential.ClientId,
                ["client_secret"] = credential.ClientSecret ?? string.Empty
            });
            string url = $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/oauth2/token";
            await ConcurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            HttpResponseMessage? response = null;
            try {
                response = await client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
                if ((int)response.StatusCode == 429) {
                    TimeSpan delay = GetRetryAfterDelay(response);
                    response.Dispose();
                    response = null;
                    if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    response = await client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
                }
                if (!response.IsSuccessStatusCode) {
                    throw new GraphApiException(response.StatusCode, AuthenticationFailure, string.Empty);
                }
#if NET5_0_OR_GREATER
                string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement token = document.RootElement;
                string? accessToken = token.GetProperty("access_token").GetString();
                string? tokenType = token.GetProperty("token_type").GetString();
                if (string.IsNullOrWhiteSpace(accessToken) || !string.Equals(tokenType, "Bearer", StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidOperationException(AuthenticationFailure);
                }
                DateTimeOffset expiresOn = DateTimeOffset.MinValue;
                if (token.TryGetProperty("expires_on", out JsonElement absolute) && TryReadSeconds(absolute, out long unix)) {
                    expiresOn = DateTimeOffset.FromUnixTimeSeconds(unix);
                }
                if (token.TryGetProperty("expires_in", out JsonElement relative) && TryReadSeconds(relative, out long seconds)) {
                    DateTimeOffset relativeExpiry = DateTimeOffset.UtcNow.AddSeconds(seconds);
                    if (expiresOn == DateTimeOffset.MinValue || relativeExpiry < expiresOn) expiresOn = relativeExpiry;
                }
                cancellationToken.ThrowIfCancellationRequested();
                return new GraphAuthorization { AccessToken = accessToken!, TokenType = "Bearer", ExpiresOn = expiresOn };
            } finally {
                response?.Dispose();
                ConcurrencySemaphore.Release();
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw new OperationCanceledException("Microsoft Graph authentication canceled.", cancellationToken);
        } catch (GraphApiException ex) {
            throw new GraphApiException(ex.StatusCode, AuthenticationFailure, string.Empty);
        } catch (HttpRequestException ex) {
            // Preserve HTTP retry classification without retaining provider messages or inner exceptions.
#if NET5_0_OR_GREATER
            throw new HttpRequestException(AuthenticationFailure, null, ex.StatusCode);
#else
            if (!Helpers.IsTransient(ex)) throw new InvalidOperationException(AuthenticationFailure);
            throw new HttpRequestException(AuthenticationFailure);
#endif
        } catch (Exception) {
            // Exception messages, response bodies and inner exceptions can contain credentials.
            throw new InvalidOperationException(AuthenticationFailure);
        }
    }

    private static bool TryReadSeconds(JsonElement value, out long seconds) =>
        long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds);
}
