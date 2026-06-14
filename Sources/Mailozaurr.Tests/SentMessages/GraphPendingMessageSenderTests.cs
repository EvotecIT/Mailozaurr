using MimeKit;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Mailozaurr.Tests.SentMessages;

public sealed class GraphPendingMessageSenderTests {
    private sealed class TestHandler : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }

    [Fact]
    public async Task SendAsync_UsesStoredAccessTokenAndUserId() {
        var requests = new List<HttpRequestMessage>();
        using var client = new HttpClient(new TestHandler((request, cancellationToken) => {
            requests.Add(request);

            if (request.RequestUri!.AbsoluteUri.EndsWith("/messages", StringComparison.OrdinalIgnoreCase)) {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) {
                    Content = new StringContent("{\"id\":\"draft-1\"}", Encoding.UTF8, "application/json")
                });
            }

            if (request.RequestUri.AbsoluteUri.EndsWith("/send", StringComparison.OrdinalIgnoreCase)) {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted) {
                    Content = new StringContent(string.Empty, Encoding.UTF8, "text/plain")
                });
            }

            throw new InvalidOperationException("Unexpected Graph request: " + request.RequestUri);
        })) {
            BaseAddress = new Uri("https://graph.microsoft.com/v1.0/")
        };

        var sender = new GraphPendingMessageSender(credential => new GraphApiClient(client, credential: credential));
        var record = await CreateRecordAsync();
        record.ProviderData[GraphPendingMessageSender.UserIdKey] = "shared@example.com";
        record.ProviderData[GraphPendingMessageSender.UserNameKey] = "shared@example.com";
        record.ProviderData[GraphPendingMessageSender.AccessTokenProtectedKey] = CredentialProtection.Default.Protect("graph-token");
        record.ProviderData[GraphPendingMessageSender.ExpiresOnKey] = DateTimeOffset.UtcNow.AddHours(1).ToString("o", CultureInfo.InvariantCulture);

        await sender.SendAsync(record, CancellationToken.None);

        Assert.Equal(2, requests.Count);
        Assert.Equal("Bearer graph-token", requests[0].Headers.Authorization?.ToString());
        Assert.Contains("/users/shared%40example.com/messages", requests[0].RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("/users/shared%40example.com/messages/draft-1/send", requests[1].RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_ReacquiresTokenWhenCredentialMetadataIsPresent() {
        var requests = new List<HttpRequestMessage>();
        using var client = new HttpClient(new TestHandler((request, cancellationToken) => {
            requests.Add(request);

            if (request.RequestUri!.AbsoluteUri.EndsWith("/messages", StringComparison.OrdinalIgnoreCase)) {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) {
                    Content = new StringContent("{\"id\":\"draft-2\"}", Encoding.UTF8, "application/json")
                });
            }

            if (request.RequestUri.AbsoluteUri.EndsWith("/send", StringComparison.OrdinalIgnoreCase)) {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted) {
                    Content = new StringContent(string.Empty, Encoding.UTF8, "text/plain")
                });
            }

            throw new InvalidOperationException("Unexpected Graph request: " + request.RequestUri);
        })) {
            BaseAddress = new Uri("https://graph.microsoft.com/v1.0/")
        };

        GraphCredential? acquired = null;
        var sender = new GraphPendingMessageSender(
            credential => new GraphApiClient(client, credential: credential),
            (graphCredential, cancellationToken) => {
                acquired = graphCredential;
                return Task.FromResult("fresh-token");
            });

        var record = await CreateRecordAsync();
        record.ProviderData[GraphPendingMessageSender.UserIdKey] = "shared@example.com";
        record.ProviderData[GraphPendingMessageSender.UserNameKey] = "shared@example.com";
        record.ProviderData[GraphPendingMessageSender.AccessTokenProtectedKey] = CredentialProtection.Default.Protect("expired-token");
        record.ProviderData[GraphPendingMessageSender.ExpiresOnKey] = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o", CultureInfo.InvariantCulture);
        record.ProviderData[GraphPendingMessageSender.ClientIdKey] = "client-id";
        record.ProviderData[GraphPendingMessageSender.TenantIdKey] = "tenant-id";
        record.ProviderData[GraphPendingMessageSender.ClientSecretProtectedKey] = CredentialProtection.Default.Protect("client-secret");

        await sender.SendAsync(record, CancellationToken.None);

        Assert.NotNull(acquired);
        Assert.Equal("client-id", acquired!.ClientId);
        Assert.Equal("tenant-id", acquired.DirectoryId);
        Assert.Equal("client-secret", acquired.ClientSecret);
        Assert.Equal("Bearer fresh-token", requests[0].Headers.Authorization?.ToString());
        var storedToken = CredentialProtection.UnprotectWithFallback(record.ProviderData[GraphPendingMessageSender.AccessTokenProtectedKey]);
        Assert.Equal("fresh-token", storedToken);
    }

    private static async Task<PendingMessageRecord> CreateRecordAsync() {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.To.Add(MailboxAddress.Parse("recipient@example.com"));
        message.Subject = "queued-graph";
        message.Body = new TextPart("plain") { Text = "body" };

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream);

        return new PendingMessageRecord {
            Provider = EmailProvider.Graph,
            MessageId = Guid.NewGuid().ToString("N"),
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10),
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            MimeMessage = Convert.ToBase64String(stream.ToArray())
        };
    }
}