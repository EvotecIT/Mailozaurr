using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

/// <summary>
/// Tests for the SES client asynchronous email sending logic.
/// </summary>
public class SesClientSendEmailAsyncTests {
    private static byte[] HmacSha256(byte[] key, string data) {
        using HMACSHA256 hmac = new(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string Sha256Hex(string data) {
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string ExpectedAuthorization(string accessKey, string secretKey, string region, string amzDate, string body) {
        string service = "ses";
        string dateStamp = amzDate.Substring(0, 8);
        string canonicalHeaders = $"content-type:application/x-www-form-urlencoded\nhost:email.{region}.amazonaws.com\nx-amz-date:{amzDate}\n";
        string signedHeaders = "content-type;host;x-amz-date";
        string payloadHash = Sha256Hex(body);
        string canonicalRequest = $"POST\n/\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        string credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
        string stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{Sha256Hex(canonicalRequest)}";
        byte[] kDate = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + secretKey), dateStamp);
        byte[] kRegion = HmacSha256(kDate, region);
        byte[] kService = HmacSha256(kRegion, service);
        byte[] kSigning = HmacSha256(kService, "aws4_request");
        byte[] sigBytes = HmacSha256(kSigning, stringToSign);
        string signature = BitConverter.ToString(sigBytes).Replace("-", string.Empty).ToLowerInvariant();
        return $"AWS4-HMAC-SHA256 Credential={accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
    }

    private static SesClient CreateClient(HttpMessageHandler handler) {
        return new SesClient(handler) {
            Credentials = new NetworkCredential("AKID", "SECRET"),
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Subject = "subject",
            Text = "body",
            RetryDelayMilliseconds = 0,
            Region = "us-east-1"
        };
    }

    [Fact]
    public async Task SendEmailAsync_ComputesSignature() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(
                "<SendRawEmailResponse><SendRawEmailResult><MessageId>ses-123</MessageId></SendRawEmailResult></SendRawEmailResponse>")
        });
        using var client = CreateClient(handler);
        client.WebhookUrl = null;

        var result = await client.SendEmailAsync();

        Assert.True(result.Status);
        Assert.Equal("ses-123", result.MessageId);
        var request = Assert.Single(handler.Requests);
        string amzDate = request.Headers.GetValues("x-amz-date").Single();
        string auth = request.Headers.GetValues("Authorization").Single();
        string body = await request.Content!.ReadAsStringAsync();
        string expected = ExpectedAuthorization("AKID", "SECRET", "us-east-1", amzDate, body);
        Assert.Equal(expected, auth);
    }

    [Fact]
    public async Task SendEmailAsync_WithToken_Succeeds() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        using var client = CreateClient(handler);
        client.WebhookUrl = null;

        using var cts = new CancellationTokenSource();
        var result = await client.SendEmailAsync(cts.Token);

        Assert.True(result.Status);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SendEmailAsync_RetriesFailedRequest() {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("fail") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        using var client = CreateClient(handler);
        client.RetryCount = 1;
        client.WebhookUrl = null;

        var result = await client.SendEmailAsync();

        Assert.True(result.Status);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task SendEmailAsync_PermanentFailureDoesNotRetry() {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("invalid") });
        using var client = CreateClient(handler);
        client.RetryCount = 3;

        var result = await client.SendEmailAsync();

        Assert.False(result.Status);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SendEmailAsync_PostsWebhook_OnSuccess() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        using var client = CreateClient(handler);
        client.WebhookUrl = "http://localhost";

        await client.SendEmailAsync();

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains(handler.Requests, r => r.RequestUri!.ToString() == "http://localhost/");
    }

    [Fact]
    public async Task SendEmailAsync_PostsWebhook_OnFailure() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("bad") });
        using var client = CreateClient(handler);
        client.WebhookUrl = "http://localhost";
        client.RetryCount = 0;

        await client.SendEmailAsync();

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains(handler.Requests, r => r.RequestUri!.ToString() == "http://localhost/");
    }

    [Fact]
    public async Task SendEmailAsync_AfterDispose_ThrowsObjectDisposedException() {
        var client = CreateClient(new RecordingHandler());
        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => client.SendEmailAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => client.SendTemplatedEmailAsync());
    }
}
