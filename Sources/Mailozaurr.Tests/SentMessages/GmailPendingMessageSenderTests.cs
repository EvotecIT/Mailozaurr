using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Runtime.CompilerServices;
using System.IO;
using MimeKit;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;

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

    [Fact]
    public async Task ProcessAsync_MintsServiceAccountTokenAndSendsMessage() {
        var mintedToken = "service-access-token";
        var tokenRequestCount = 0;
        var tokenHandler = new TestHandler(async (request, cancellationToken) => {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri("https://oauth2.googleapis.com/token"), request.RequestUri);
#if NET5_0_OR_GREATER
            var payload = await request.Content!.ReadAsStringAsync(cancellationToken);
#else
            var payload = await request.Content!.ReadAsStringAsync();
#endif
            Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer", payload);
            Assert.Contains("assertion=", payload);
            Interlocked.Increment(ref tokenRequestCount);
            var response = new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{\"access_token\":\"service-access-token\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
            };
            return response;
        });

        string? authorization = null;
        var gmailCallCount = 0;
        var gmailHandler = new TestHandler((request, _) => {
            authorization = request.Headers.Authorization?.ToString();
            Interlocked.Increment(ref gmailCallCount);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{\"id\":\"msg\",\"threadId\":\"msg\"}", Encoding.UTF8, "application/json")
            });
        });

        using var gmailClient = new HttpClient(gmailHandler) { BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/") };
        var sender = new GmailPendingMessageSender((credential, refresher) => new GmailApiClient(gmailClient, refresher, credential));

        var serviceAccountJson = CreateServiceAccountJson();
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.To.Add(MailboxAddress.Parse("recipient@example.com"));
        message.Subject = "queued";
        message.Body = new TextPart("plain") { Text = "body" };
        using var ms = new MemoryStream();
        await message.WriteToAsync(ms);

        var now = DateTimeOffset.UtcNow;
        var record = new PendingMessageRecord {
            MessageId = Guid.NewGuid().ToString("N"),
            MimeMessage = Convert.ToBase64String(ms.ToArray()),
            Provider = EmailProvider.Gmail,
            Timestamp = now.AddMinutes(-10),
            NextAttemptAt = now.AddMinutes(-1)
        };
        var subject = "impersonated@example.com";
        record.ProviderData[GmailPendingMessageSender.UserIdKey] = subject;
        record.ProviderData[GmailPendingMessageSender.UserNameKey] = subject;
        record.ProviderData[GmailPendingMessageSender.AccessTokenProtectedKey] = CredentialProtection.Default.Protect("expired-token");
        record.ProviderData[GmailPendingMessageSender.ExpiresOnKey] = now.AddMinutes(-5).ToString("o", CultureInfo.InvariantCulture);
        record.ProviderData[GmailPendingMessageSender.ServiceAccountJsonProtectedKey] = CredentialProtection.Default.Protect(serviceAccountJson);
        record.ProviderData[GmailPendingMessageSender.ServiceAccountSubjectKey] = subject;

        var repository = new SingleRecordRepository(record);
        var observer = new ProcessorObserver();
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.Gmail, sender }
        });

        Helpers.SharedHttpClient = new HttpClient(tokenHandler);

        try {
            var processor = new PendingMessageProcessor(
                repository,
                factory,
                retryDelaySelector: _ => TimeSpan.FromMinutes(1),
                clock: () => now,
                observer: observer,
                processingLeaseDuration: TimeSpan.Zero);

            await processor.ProcessAsync();
        } finally {
            Helpers.SharedHttpClient = new HttpClient();
        }

        Assert.Equal(1, tokenRequestCount);
        Assert.Equal(1, gmailCallCount);
        Assert.Equal("Bearer service-access-token", authorization);
        Assert.True(repository.IsEmpty);
        Assert.Equal(1, observer.SentCount);
        Assert.Equal(0, observer.FailedCount);
        Assert.Equal(0, observer.DroppedCount);

        var storedAccess = CredentialProtection.UnprotectWithFallback(record.ProviderData[GmailPendingMessageSender.AccessTokenProtectedKey]);
        Assert.Equal(mintedToken, storedAccess);
        var storedExpiry = DateTimeOffset.Parse(record.ProviderData[GmailPendingMessageSender.ExpiresOnKey], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        Assert.True(storedExpiry > now);
    }

    private static string CreateServiceAccountJson() {
        var generator = new RsaKeyPairGenerator();
        generator.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
        AsymmetricCipherKeyPair keyPair = generator.GenerateKeyPair();
        var privateKeyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(keyPair.Private);
        var builder = new StringBuilder();
        builder.AppendLine("-----BEGIN PRIVATE KEY-----");
        builder.AppendLine(Convert.ToBase64String(privateKeyInfo.GetEncoded(), Base64FormattingOptions.InsertLineBreaks));
        builder.AppendLine("-----END PRIVATE KEY-----");

        var payload = new Dictionary<string, object> {
            { "type", "service_account" },
            { "project_id", "test-project" },
            { "private_key_id", "test-key" },
            { "private_key", builder.ToString() },
            { "client_email", "mailer@test-project.iam.gserviceaccount.com" },
            { "client_id", "1234567890" },
            { "token_uri", "https://oauth2.googleapis.com/token" }
        };

        return JsonSerializer.Serialize(payload);
    }

    private sealed class SingleRecordRepository : IPendingMessageRepository {
        private PendingMessageRecord? record;

        public SingleRecordRepository(PendingMessageRecord record) {
            this.record = record ?? throw new ArgumentNullException(nameof(record));
        }

        public bool IsEmpty => record == null;

        public Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) {
            this.record = record;
            return Task.CompletedTask;
        }

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(record);

        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var current = record;
            if (current != null) {
                cancellationToken.ThrowIfCancellationRequested();
                yield return current;
            }
            await Task.CompletedTask;
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
            var current = record;
            if (current != null && string.Equals(current.MessageId, messageId, StringComparison.Ordinal)) {
                record = null;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class ProcessorObserver : IPendingMessageProcessorObserver {
        public int SentCount { get; private set; }
        public int FailedCount { get; private set; }
        public int DroppedCount { get; private set; }

        public void MessageSkipped(PendingMessageRecord record, PendingMessageSkipReason reason) {
        }

        public void MessageAttemptStarted(PendingMessageRecord record, int attempt) {
        }

        public void MessageSent(PendingMessageRecord record, int attempt, TimeSpan duration) {
            SentCount++;
        }

        public void MessageFailed(
            PendingMessageRecord record,
            int attempt,
            Exception exception,
            TimeSpan duration,
            bool willRetry,
            TimeSpan? retryDelay) {
            FailedCount++;
        }

        public void MessageDropped(PendingMessageRecord record, int attempt, PendingMessageDropReason reason, Exception? exception) {
            DroppedCount++;
        }
    }
}
