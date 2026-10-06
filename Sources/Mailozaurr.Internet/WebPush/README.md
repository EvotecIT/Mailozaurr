# Browser Web Push

Mailozaurr.Internet provides Web Push encryption, VAPID signing, and HTTP delivery on .NET 8 or later. The older .NET Framework and .NET Standard targets retain their existing APIs; they do not expose Web Push.

Create a VAPID key pair once with `WebPush.NewKeyPair()`. Keep the private key in protected application storage and include it in recovery backups. Browsers subscribe using the public key and return an endpoint, `p256dh`, and `auth` values.

```csharp
using Mailozaurr;

using var sender = new WebPushClient(); // Reuse for the application lifetime.
var subscription = new WebPushSubscription(endpoint, browserPublicKey, browserAuth);
var credentials = new WebPushCredentials(vapidPublicKey, vapidPrivateKey, "mailto:ops@example.com");
WebPushResult result = await sender.SendAsync(subscription, credentials,
    payloadUtf8, TimeSpan.FromDays(1), WebPushUrgency.Normal, stoppingToken);
```

The default sender disables redirects and accepts HTTPS endpoints for supported browser push services. It encrypts a payload of up to 3,000 bytes and sends it with a TTL between zero and 28 days. A custom transport is trusted and must preserve the same redirect and destination restrictions.

For applications using `IHttpClientFactory`, pass the configured client to `new WebPushClient(httpClient)`. This overload preserves the client's timeout and headers and leaves disposal to its caller. Configure that client's handler with redirects disabled. The handler overload instead transfers transport ownership to the sender and uses its 30-second timeout.

Applications own consent, subscription persistence, payload content, and retry scheduling. Remove expired subscriptions after HTTP 404 or 410. Other unsuccessful status codes and the returned `RetryAfterDate` or `RetryAfterDelay` let the application schedule a bounded retry. HTTP success means the push service accepted the request; it does not prove that a notification appeared on a device. Caller cancellation and transport exceptions propagate.
