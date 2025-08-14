using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class SesClientSendTemplatedEmailAsyncTests {
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
            TemplateName = "MyTemplate",
            TemplateData = new Dictionary<string, string> { ["Name"] = "John" },
            RetryDelayMilliseconds = 0,
            Region = "us-east-1"
        };
    }

    [Fact]
    public async Task SendTemplatedEmailAsync_ComputesSignature() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        using var client = CreateClient(handler);
        client.WebhookUrl = null;

        var result = await client.SendTemplatedEmailAsync();

        Assert.True(result.Status);
        var request = Assert.Single(handler.Requests);
        string amzDate = request.Headers.GetValues("x-amz-date").Single();
        string auth = request.Headers.GetValues("Authorization").Single();
        string body = await request.Content!.ReadAsStringAsync();
        string expected = ExpectedAuthorization("AKID", "SECRET", "us-east-1", amzDate, body);
        Assert.Equal(expected, auth);
        Assert.Contains("Template=MyTemplate", body);
        string expectedJson = JsonSerializer.Serialize(client.TemplateData);
        Assert.Contains("TemplateData=" + Uri.EscapeDataString(expectedJson), body);
    }

    [Fact]
    public async Task SendTemplatedEmailAsync_EncodesMultipleParameters() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        using var client = new SesClient(handler) {
            Credentials = new NetworkCredential("AKID", "SECRET"),
            From = "sender@example.com",
            To = new List<object> { "to1@example.com", "to2@example.com" },
            TemplateName = "MyTemplate",
            TemplateData = new Dictionary<string, string> { ["Name"] = "John", ["Code"] = "123" },
            RetryDelayMilliseconds = 0,
            Region = "us-east-1"
        };
        client.WebhookUrl = null;

        await client.SendTemplatedEmailAsync();

        var request = Assert.Single(handler.Requests);
        string body = await request.Content!.ReadAsStringAsync();
        Assert.Contains("Destination.ToAddresses.member.1=to1%40example.com", body);
        Assert.Contains("Destination.ToAddresses.member.2=to2%40example.com", body);
        string expectedJson = JsonSerializer.Serialize(client.TemplateData);
        Assert.Contains("TemplateData=" + Uri.EscapeDataString(expectedJson), body);
    }
}

