using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using MimeKit;

namespace Mailozaurr.Tests.SentMessages;

public sealed class GmailPendingMessageSenderTests {
    private sealed class TestHandler : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

        public TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }

    [Fact]
    public async Task SendAsync_UsesAccessTokenAndUserId() {
        Uri? requestUri = null;
        string? authorization = null;
        var handler = new TestHandler((request, _) => {
            requestUri = request.RequestUri;
            authorization = request.Headers.Authorization?.ToString();
            var response = new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{\"id\":\"msg\",\"threadId\":\"msg\"}", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });
        using var httpClient = new HttpClient(handler) {
            BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/")
        };

        var sender = new GmailPendingMessageSender(credential => {
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credential.AccessToken);
            return new GmailApiClient(httpClient);
        });

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
        record.ProviderData[GmailPendingMessageSender.UserIdKey] = "me";
        record.ProviderData[GmailPendingMessageSender.AccessTokenKey] = "token";
        record.ProviderData[GmailPendingMessageSender.UserNameKey] = "me@example.com";
        record.ProviderData[GmailPendingMessageSender.ExpiresOnKey] = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        await sender.SendAsync(record, CancellationToken.None);

        Assert.Equal("Bearer token", authorization);
        Assert.Equal(new Uri("https://gmail.googleapis.com/gmail/v1/users/me/messages/send"), requestUri);
    }
}
