#if NET8_0_OR_GREATER

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mailozaurr;

namespace Mailozaurr.Tests;

public sealed class WebPushTests {
    [Fact]
    public void DiagnosticStrings_DoNotExposeCredentialsOrSubscriptionSecrets() {
        var credentials = new WebPushCredentials("public-key", "private-secret", "mailto:ops@example.com");
        var subscription = new WebPushSubscription(new Uri("https://fcm.googleapis.com/secret-token"), "browser-key", "auth-secret");
        Assert.Equal("WebPushCredentials", $"{credentials}");
        Assert.Equal("WebPushSubscription", $"{subscription}");
    }

    [Fact]
    public async Task Sender_EncryptsAndSigns_AndReturnsExpiredSubscriptionStatus() {
        (string publicKey,string privateKey) = WebPush.NewKeyPair();
        using ECDiffieHellman browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var point = browser.ExportParameters(false).Q;
        var subscription = new WebPushSubscription(new Uri("https://fcm.googleapis.com/fcm/send/abc"),
            WebPush.EncodeBase64Url([0x04,..point.X!,..point.Y!]),WebPush.EncodeBase64Url(RandomNumberGenerator.GetBytes(16)));
        int calls = 0;
        using var sender = new WebPushClient(new PushHandler(async (request, token) => {
            calls++;
            Assert.Equal(subscription.Endpoint,request.RequestUri);
            Assert.Equal("high", Assert.Single(request.Headers.GetValues("Urgency")));
            Assert.Equal("86400",Assert.Single(request.Headers.GetValues("TTL")));
            Assert.StartsWith("vapid t=",Assert.Single(request.Headers.GetValues("Authorization")),StringComparison.Ordinal);
            Assert.Equal("aes128gcm",Assert.Single(request.Content!.Headers.ContentEncoding));
            byte[] body = await request.Content.ReadAsByteArrayAsync(token);
            Assert.True(body.Length > 100);
            Assert.DoesNotContain("Tomorrow: BIO",Encoding.UTF8.GetString(body),StringComparison.Ordinal);
            var response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Gone);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(5));
            return response;
        }));
        var credentials = new WebPushCredentials(publicKey,privateKey,"mailto:ops@example.com");
        var result = await sender.SendAsync(subscription, credentials, "Tomorrow: BIO"u8.ToArray(), TimeSpan.FromDays(1), WebPushUrgency.High);
        Assert.Equal(System.Net.HttpStatusCode.Gone, result.StatusCode);
        Assert.Equal(TimeSpan.FromMinutes(5), result.RetryAfterDelay);
        await Assert.ThrowsAsync<ArgumentException>(() => sender.SendAsync(subscription with { Endpoint=new Uri("https://127.0.0.1/private") },credentials,[],TimeSpan.Zero));
        Assert.Equal(1,calls);
    }

    private sealed class PushHandler(Func<System.Net.Http.HttpRequestMessage,CancellationToken,Task<System.Net.Http.HttpResponseMessage>> send) : System.Net.Http.HttpMessageHandler {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken token) => send(request,token);
    }

    [Fact]
    public void RejectsMalformedP256Point() {
        Assert.False(WebPush.IsBrowserKey([0x04, .. new byte[64]]));
    }
    private static byte[] B64(string value) => WebPush.DecodeBase64Url(value);

    [Fact]
    public void Encrypt_MatchesTheRfc8291Example() {
        // RFC 8291 Appendix A: fixed keys, auth secret, and salt give one exact message.
        byte[] asPublic = B64("BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8");
        using ECDiffieHellman server = ECDiffieHellman.Create(new ECParameters {
            Curve = ECCurve.NamedCurves.nistP256,
            D = B64("yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw"),
            Q = new ECPoint { X = asPublic[1..33], Y = asPublic[33..65] }
        });

        byte[] message = WebPush.Encrypt(Encoding.UTF8.GetBytes("When I grow up, I want to be a watermelon"),
            B64("BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4"),
            B64("BTBZMqHH6r4Tts7J_aSIgg"), server, B64("DGv6ra1nlYgDCS1FRnbzlw"));

        // The RFC prints the header and the ciphertext separately; the message is the one followed by the other.
        byte[] header = B64("DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8");
        Assert.Equal(header, message[..header.Length]);
        Assert.Equal(B64("8pfeW0KbunFT06SuDKoJH9Ql87S1QUrdirN6GcG7sFz1y1sqLgVi1VhjVkHsUoEsbI_0LpXMuGvnzQ"), message[header.Length..]);
    }

    [Fact]
    public void Authorization_IsAVapidTokenThePublicKeyVerifies() {
        (string publicKey, string privateKey) = WebPush.NewKeyPair();
        DateTimeOffset now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

        string value = WebPush.Authorization(new Uri("https://fcm.googleapis.com/fcm/send/abc"), "mailto:ops@example.com", publicKey, privateKey, now);

        Assert.StartsWith("vapid t=", value, StringComparison.Ordinal);
        Assert.EndsWith($", k={publicKey}", value, StringComparison.Ordinal);
        string[] token = value["vapid t=".Length..value.IndexOf(',')].Split('.');
        using JsonDocument claims = JsonDocument.Parse(B64(token[1]));
        Assert.Equal("https://fcm.googleapis.com", claims.RootElement.GetProperty("aud").GetString());
        Assert.Equal(now.AddHours(12).ToUnixTimeSeconds(), claims.RootElement.GetProperty("exp").GetInt64());
        byte[] point = B64(publicKey);
        using ECDsa verifier = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = point[1..33], Y = point[33..65] } });
        Assert.True(verifier.VerifyData(Encoding.ASCII.GetBytes($"{token[0]}.{token[1]}"), B64(token[2]), HashAlgorithmName.SHA256));
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc", true)]
    [InlineData("https://wns2-par02p.notify.windows.com/w/?token=abc", true)]
    [InlineData("https://web.push.apple.com/abc", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc", false)]
    [InlineData("https://fcm.googleapis.com:8443/abc", false)]
    [InlineData("https://notify.windows.com.evil.example/abc", false)]
    [InlineData("https://169.254.169.254/latest/meta-data", false)]
    public void IsPushService_AcceptsOnlyBrowserPushServices(string endpoint, bool accepted) =>
        Assert.Equal(accepted, WebPush.IsPushService(new Uri(endpoint)));
}

#endif
