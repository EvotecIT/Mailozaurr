using System.Net;
using System.Net.Http;
using System.Text;

namespace Mailozaurr.Tests.SentMessages;

public sealed class SendGridPendingMessageSenderTests {
    private sealed class TestHandler : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

        public TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) {
            this.handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }

    [Fact]
    public async Task SendAsync_PostsJsonPayloadWithBearerToken() {
        Uri? requestUri = null;
        string? authorization = null;
        string? payload = null;
        var handler = new TestHandler(async (request, _) => {
            requestUri = request.RequestUri;
            authorization = request.Headers.Authorization?.ToString();
            payload = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent(string.Empty) };
        });
        using var httpClient = new HttpClient(handler);
        var sender = new SendGridPendingMessageSender(httpClient);

        var json = "{\"personalizations\":[]}";
        var record = new PendingMessageRecord();
        record.ProviderData[SendGridPendingMessageSender.MessageJsonKey] = json;
        record.ProviderData[SendGridPendingMessageSender.ApiKeyBase64Key] = Convert.ToBase64String(Encoding.UTF8.GetBytes("SG.API"));

        await sender.SendAsync(record, CancellationToken.None);

        Assert.Equal(new Uri("https://api.sendgrid.com/v3/mail/send"), requestUri);
        Assert.Equal("Bearer SG.API", authorization);
        Assert.Equal(json, payload);
    }
}