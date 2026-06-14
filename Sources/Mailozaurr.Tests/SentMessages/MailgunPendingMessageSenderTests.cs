using MimeKit;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Mailozaurr.Tests.SentMessages;

public sealed class MailgunPendingMessageSenderTests {
    private sealed class TestHandler : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

        public TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) {
            if (handler == null) {
                throw new ArgumentNullException(nameof(handler));
            }
            this.handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }

    [Fact]
    public async Task SendAsync_PostsMimeMessageWithBasicAuth() {
        Uri? requestUri = null;
        string? authorization = null;
        string? payload = null;
        var handler = new TestHandler(async (request, _) => {
            requestUri = request.RequestUri;
            authorization = request.Headers.Authorization?.ToString();
            payload = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(string.Empty)
            };
        });
        using var httpClient = new HttpClient(handler);
        var sender = new MailgunPendingMessageSender(httpClient);

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.To.Add(MailboxAddress.Parse("recipient@example.com"));
        message.Subject = "queued";
        message.Body = new TextPart("plain") { Text = "body" };
        message.MessageId = "mailgun-message";
        using var ms = new MemoryStream();
        await message.WriteToAsync(ms);

        var record = new PendingMessageRecord {
            MessageId = message.MessageId ?? string.Empty,
            MimeMessage = Convert.ToBase64String(ms.ToArray())
        };
        record.ProviderData[MailgunPendingMessageSender.DomainKey] = "example.com";
        record.ProviderData[MailgunPendingMessageSender.ApiKeyKey] = "key";

        await sender.SendAsync(record, CancellationToken.None);

        Assert.Equal(new Uri("https://api.mailgun.net/v3/example.com/messages.mime"), requestUri);
        var expectedAuth = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("api:key"));
        Assert.Equal(expectedAuth, authorization);
        Assert.Contains("mailgun-message", payload);
        Assert.Contains("body", payload);
    }
}