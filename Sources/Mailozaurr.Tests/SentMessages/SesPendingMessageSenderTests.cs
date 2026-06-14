using MimeKit;
using System.Net;
using System.Net.Http;
using System.Text;

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
        Uri? requestUri = null;
        var handler = new TestHandler((request, _) => {
            requestUri = request.RequestUri;
            actualAuth = request.Headers.TryGetValues("Authorization", out var authValues)
                ? authValues.Single()
                : null;
            actualDate = request.Headers.TryGetValues("x-amz-date", out var dateValues)
                ? dateValues.Single()
                : null;
            var response = new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("<SendRawEmailResponse/>")
            };
            return Task.FromResult(response);
        });
        using var httpClient = new HttpClient(handler);
        var fixedTime = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var sender = new SesPendingMessageSender(httpClient, () => fixedTime);

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.To.Add(MailboxAddress.Parse("recipient@example.com"));
        message.Subject = "queued";
        message.Body = new TextPart("plain") { Text = "body" };
        using var ms = new MemoryStream();
        await message.WriteToAsync(ms);

        var record = new PendingMessageRecord {
            MimeMessage = Convert.ToBase64String(ms.ToArray())
        };
        const string accessKey = "AKIDEXAMPLE";
        const string secretKey = "wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY";
        record.ProviderData[SesPendingMessageSender.AccessKeyIdKey] = accessKey;
        record.ProviderData[SesPendingMessageSender.SecretAccessKeyKey] = secretKey;
        record.ProviderData[SesPendingMessageSender.RegionKey] = "us-east-1";

        await sender.SendAsync(record, CancellationToken.None);

        using var expected = CreateExpectedRequest(accessKey, secretKey, "us-east-1", record.MimeMessage, fixedTime);
        var expectedAuth = expected.Headers.GetValues("Authorization").Single();
        Assert.Equal(expectedAuth, actualAuth);
        var expectedDate = expected.Headers.GetValues("x-amz-date").Single();
        Assert.Equal(expectedDate, actualDate);
        Assert.Equal(new Uri("https://email.us-east-1.amazonaws.com/"), requestUri);
    }

    private static HttpRequestMessage CreateExpectedRequest(string accessKey, string secretKey, string region, string base64Mime, DateTime now) {
        var type = typeof(SesPendingMessageSender);
        var buildBody = type.GetMethod("BuildRequestBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var body = (string)buildBody.Invoke(null, new object[] { base64Mime })!;
        var createRequest = type.GetMethod("CreateRequest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var request = (HttpRequestMessage)createRequest.Invoke(null, new object[] { accessKey, secretKey, region, body, now })!;
        request.Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        return request;
    }
}