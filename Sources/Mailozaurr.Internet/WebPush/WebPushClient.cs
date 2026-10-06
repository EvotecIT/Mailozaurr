#if NET8_0_OR_GREATER
namespace Mailozaurr;

/// <summary>A browser subscription returned by PushManager.subscribe.</summary>
/// <param name="Endpoint">HTTPS endpoint at an approved browser push service.</param>
/// <param name="PublicKey">Base64url uncompressed P-256 browser public key (p256dh).</param>
/// <param name="AuthenticationSecret">Base64url 16-byte authentication secret (auth).</param>
public sealed record WebPushSubscription(Uri Endpoint, string PublicKey, string AuthenticationSecret) {
    /// <summary>Returns a diagnostic label without subscription secrets or endpoint tokens.</summary>
    public override string ToString() => nameof(WebPushSubscription);
}

/// <summary>Installation-local VAPID signing credentials.</summary>
/// <param name="PublicKey">Base64url uncompressed P-256 application public key.</param>
/// <param name="PrivateKey">Base64url private scalar; store privately and back it up.</param>
/// <param name="Contact">Absolute mailto or HTTPS operator contact URI.</param>
public sealed record WebPushCredentials(string PublicKey, string PrivateKey, string Contact) {
    /// <summary>Returns a diagnostic label without signing credentials.</summary>
    public override string ToString() => nameof(WebPushCredentials);
}

/// <summary>Delivery priority supplied to a browser push service.</summary>
public enum WebPushUrgency {
    /// <summary>Delivery when the device is idle.</summary>
    VeryLow,
    /// <summary>Low priority delivery.</summary>
    Low,
    /// <summary>Normal priority delivery.</summary>
    Normal,
    /// <summary>Time-sensitive delivery.</summary>
    High
}

/// <summary>The push service response; retry policy remains with the calling application.</summary>
/// <param name="StatusCode">HTTP response status, including 404/410 for an expired subscription.</param>
/// <param name="RetryAfterDate">Absolute Retry-After value, when supplied.</param>
/// <param name="RetryAfterDelay">Relative Retry-After value, when supplied.</param>
public sealed record WebPushResult(HttpStatusCode StatusCode, DateTimeOffset? RetryAfterDate, TimeSpan? RetryAfterDelay);

/// <summary>Sends RFC 8291 encrypted Web Push messages on .NET 8 or later. The default transport never follows redirects.</summary>
public sealed class WebPushClient : IDisposable {
    private readonly HttpClient client;
    private readonly bool ownsClient;

    /// <summary>Creates a reusable sender with a 30-second timeout and redirects disabled.</summary>
    public WebPushClient() : this(new HttpClientHandler { AllowAutoRedirect = false }) { }

    /// <summary>Creates a sender owning the supplied transport. Custom handlers must not follow redirects or rewrite destination hosts.</summary>
    public WebPushClient(HttpMessageHandler handler) {
        ArgumentNullException.ThrowIfNull(handler);
        client = new HttpClient(handler, disposeHandler:true) { Timeout = TimeSpan.FromSeconds(30) };
        ownsClient = true;
    }

    /// <summary>Uses a caller-owned client without changing its timeout or headers. Its transport must disable redirects and must not rewrite destination hosts.</summary>
    public WebPushClient(HttpClient client) {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
    }

    /// <summary>Sends one message and returns the push service status. HTTP 404/410 mean the subscription should be removed; transport errors propagate.</summary>
    public async Task<WebPushResult> SendAsync(WebPushSubscription subscription, WebPushCredentials credentials,
        byte[] payload, TimeSpan timeToLive, WebPushUrgency urgency = WebPushUrgency.Normal, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();
        if (!WebPush.IsPushService(subscription.Endpoint)) throw new ArgumentException("Expected a browser push-service endpoint.", nameof(subscription));
        if (timeToLive < TimeSpan.Zero || timeToLive > TimeSpan.FromDays(28)) throw new ArgumentOutOfRangeException(nameof(timeToLive));
        string urgencyHeader = urgency switch {
            WebPushUrgency.VeryLow => "very-low",
            WebPushUrgency.Low => "low",
            WebPushUrgency.Normal => "normal",
            WebPushUrgency.High => "high",
            _ => throw new ArgumentOutOfRangeException(nameof(urgency))
        };
        byte[] encrypted = WebPush.Encrypt(payload,WebPush.DecodeBase64Url(subscription.PublicKey),WebPush.DecodeBase64Url(subscription.AuthenticationSecret));
        using var request = new HttpRequestMessage(HttpMethod.Post,subscription.Endpoint);
        request.Content = new ByteArrayContent(encrypted);
        request.Content.Headers.ContentType = new("application/octet-stream");
        request.Content.Headers.ContentEncoding.Add("aes128gcm");
        request.Headers.TryAddWithoutValidation("Authorization",WebPush.Authorization(subscription.Endpoint,credentials.Contact,credentials.PublicKey,credentials.PrivateKey,DateTimeOffset.UtcNow));
        request.Headers.TryAddWithoutValidation("TTL",((int)timeToLive.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation("Urgency", urgencyHeader);
        using var response = await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellationToken).ConfigureAwait(false);
        return new WebPushResult(response.StatusCode, response.Headers.RetryAfter?.Date, response.Headers.RetryAfter?.Delta);
    }

    /// <summary>Disposes the owned HTTP client and transport.</summary>
    public void Dispose() { if (ownsClient) client.Dispose(); }
}
#endif
