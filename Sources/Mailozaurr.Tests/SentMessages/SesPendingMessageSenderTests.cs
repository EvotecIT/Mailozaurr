using System.Net;
using System.Net.Http;

namespace Mailozaurr.Tests.SentMessages;

public sealed class SesPendingMessageSenderTests {
    private sealed class TestHandler : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

        public TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }

    [Fact]
    public async Task SendAsync_ComputesAwsSignature() {
        string? actualAuth = null;
        string? actualDate = null;
        string? actualBody = null;
        string? actualContentType = null;
        Uri? requestUri = null;
        var handler = new TestHandler(async (request, _) => {
            requestUri = request.RequestUri;
            actualAuth = request.Headers.TryGetValues("Authorization", out var authValues)
                ? authValues.Single()
                : null;
            actualDate = request.Headers.TryGetValues("x-amz-date", out var dateValues)
                ? dateValues.Single()
                : null;
            actualBody = await request.Content!.ReadAsStringAsync();
            actualContentType = request.Content.Headers.ContentType?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("<SendRawEmailResponse/>")
            };
        });
        using var httpClient = new HttpClient(handler);
        var fixedTime = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var sender = new SesPendingMessageSender(httpClient, () => fixedTime);

        var record = new PendingMessageRecord {
            MimeMessage = "AQID"
        };
        const string accessKey = "AKIDEXAMPLE";
        const string secretKey = "wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY";
        record.ProviderData[SesPendingMessageSender.AccessKeyIdKey] = accessKey;
        record.ProviderData[SesPendingMessageSender.SecretAccessKeyKey] = secretKey;
        record.ProviderData[SesPendingMessageSender.RegionKey] = "us-east-1";

        await sender.SendAsync(record, CancellationToken.None);

        Assert.Equal(
            "AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/20240102/us-east-1/ses/aws4_request, " +
            "SignedHeaders=content-type;host;x-amz-date, " +
            "Signature=9fb67414a234bd795c0599f48c535c5d21ab8873229b1d1052dd716297a385d0",
            actualAuth);
        Assert.Equal("20240102T030405Z", actualDate);
        Assert.Equal(new Uri("https://email.us-east-1.amazonaws.com/"), requestUri);
        Assert.Equal(
            "Action=SendRawEmail&RawMessage.Data=AQID&Version=2010-12-01",
            actualBody);
        Assert.Equal("application/x-www-form-urlencoded", actualContentType);
    }
}
