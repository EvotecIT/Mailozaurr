using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
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

        var sender = new GmailPendingMessageSender((credential, refresher) => new GmailApiClient(httpClient, refresher, credential));

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
        record.ProviderData[GmailPendingMessageSender.AccessTokenProtectedKey] = CredentialProtection.Default.Protect("token");
        record.ProviderData[GmailPendingMessageSender.UserNameKey] = "me@example.com";
        record.ProviderData[GmailPendingMessageSender.ExpiresOnKey] = DateTimeOffset.UtcNow.AddHours(1).ToString("o", CultureInfo.InvariantCulture);

        await sender.SendAsync(record, CancellationToken.None);

        Assert.Equal("Bearer token", authorization);
        Assert.Equal(new Uri("https://gmail.googleapis.com/gmail/v1/users/me/messages/send"), requestUri);
    }

    [Fact]
    public async Task SendAsync_RefreshesExpiredTokenAndSendsMessage() {
        var refreshCallCount = 0;
        var refreshHandler = new TestHandler(async (request, cancellationToken) => {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri("https://oauth2.googleapis.com/token"), request.RequestUri);
#if NET5_0_OR_GREATER
            var payload = await request.Content!.ReadAsStringAsync(cancellationToken);
#else
            var payload = await request.Content!.ReadAsStringAsync();
#endif
            Assert.Contains("client_id=client-123", payload);
            Assert.Contains("client_secret=client-secret", payload);
            Assert.Contains("refresh_token=refresh-token", payload);
            Interlocked.Increment(ref refreshCallCount);
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(
                    "{\"access_token\":\"new-access-token\",\"expires_in\":3600,\"refresh_token\":\"updated-refresh\"}",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var refreshClient = new HttpClient(refreshHandler);
        Helpers.SharedHttpClient = refreshClient;

        try {
            string? authorization = null;
            var sendCallCount = 0;
            var gmailHandler = new TestHandler((request, _) => {
                authorization = request.Headers.Authorization?.ToString();
                Interlocked.Increment(ref sendCallCount);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent("{\"id\":\"msg\",\"threadId\":\"msg\"}", Encoding.UTF8, "application/json")
                });
            });
            using var gmailClient = new HttpClient(gmailHandler) {
                BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/")
            };
            var sender = new GmailPendingMessageSender((credential, refresher) => new GmailApiClient(gmailClient, refresher, credential));

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
            record.ProviderData[GmailPendingMessageSender.UserNameKey] = "user@example.com";
            record.ProviderData[GmailPendingMessageSender.ExpiresOnKey] = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o", CultureInfo.InvariantCulture);
            record.ProviderData[GmailPendingMessageSender.AccessTokenProtectedKey] = CredentialProtection.Default.Protect("expired-token");
            record.ProviderData[GmailPendingMessageSender.RefreshTokenProtectedKey] = CredentialProtection.Default.Protect("refresh-token");
            record.ProviderData[GmailPendingMessageSender.ClientIdKey] = "client-123";
            record.ProviderData[GmailPendingMessageSender.ClientSecretProtectedKey] = CredentialProtection.Default.Protect("client-secret");

            await sender.SendAsync(record, CancellationToken.None);

            Assert.Equal(1, refreshCallCount);
            Assert.Equal(1, sendCallCount);
            Assert.Equal("Bearer new-access-token", authorization);

            var storedAccess = CredentialProtection.UnprotectWithFallback(record.ProviderData[GmailPendingMessageSender.AccessTokenProtectedKey]);
            Assert.Equal("new-access-token", storedAccess);
            var storedRefresh = CredentialProtection.UnprotectWithFallback(record.ProviderData[GmailPendingMessageSender.RefreshTokenProtectedKey]);
            Assert.Equal("updated-refresh", storedRefresh);
            var storedExpiry = DateTimeOffset.Parse(record.ProviderData[GmailPendingMessageSender.ExpiresOnKey], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Assert.True(storedExpiry > DateTimeOffset.UtcNow);
        } finally {
            Helpers.SharedHttpClient = new HttpClient();
        }
    }
}
