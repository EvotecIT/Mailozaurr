using MimeKit;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests.SentMessages;

public sealed class ProviderPendingMessageTests {
    private sealed class InMemoryPendingMessageRepository : IPendingMessageRepository {
        private readonly Dictionary<string, PendingMessageRecord> records = new(StringComparer.OrdinalIgnoreCase);

        public PendingMessageRecord? LastSaved { get; private set; }

        public Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            LastSaved = record;
            var key = record.MessageId ?? throw new InvalidOperationException("Pending message requires an identifier.");
            records[key] = record;
            return Task.CompletedTask;
        }

        public Task<PendingMessageRecord?> TryAcquireLeaseAsync(
            string messageId,
            DateTimeOffset dueBeforeOrAt,
            DateTimeOffset leaseUntil,
            CancellationToken cancellationToken = default) {
            if (messageId == null) {
                throw new ArgumentNullException(nameof(messageId));
            }

            if (!records.TryGetValue(messageId, out var record) || record.NextAttemptAt > dueBeforeOrAt) {
                return Task.FromResult<PendingMessageRecord?>(null);
            }

            record.NextAttemptAt = leaseUntil;
            return Task.FromResult<PendingMessageRecord?>(record);
        }

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) {
            if (messageId == null) {
                throw new ArgumentNullException(nameof(messageId));
            }

            records.TryGetValue(messageId, out var record);
            return Task.FromResult<PendingMessageRecord?>(record);
        }

        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default) {
            foreach (var record in records.Values.ToArray()) {
                cancellationToken.ThrowIfCancellationRequested();
                yield return record;
                await Task.Yield();
            }
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
            if (messageId == null) {
                throw new ArgumentNullException(nameof(messageId));
            }

            records.Remove(messageId);
            return Task.CompletedTask;
        }
    }

    private sealed class TestHandler : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

        public int CallCount { get; private set; }

        public TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            CallCount++;
            return await handler(request, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class DelayedCancellationHandler : HttpMessageHandler {
        private readonly TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitForStartAsync() => started.Task;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Unreachable");
        }
    }

    private sealed class CancellationOnSavePendingMessageRepository : IPendingMessageRepository {
        private readonly TaskCompletionSource<bool> saveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitForSaveStartAsync() => saveStarted.Task;

        public async Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) {
            saveStarted.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }

        public Task<PendingMessageRecord?> TryAcquireLeaseAsync(
            string messageId,
            DateTimeOffset dueBeforeOrAt,
            DateTimeOffset leaseUntil,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PendingMessageRecord?>(null);

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PendingMessageRecord?>(null);

        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default) {
            await Task.CompletedTask;
            yield break;
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task SendGridClientQueuesAndProcessesPendingMessage() {
        using var client = new SendGridClient {
            Credentials = new NetworkCredential("apikey", "SG.API"),
            From = "sender@example.com",
            To = new List<object> { "recipient@example.com" },
            Subject = "queued",
            Text = "hello"
        };
        client.CreateMessage();

        var repository = new InMemoryPendingMessageRepository();
        client.PendingMessageRepository = repository;

        var failureHandler = new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) {
            Content = new StringContent("failure", Encoding.UTF8, "text/plain")
        }));
        var httpClientField = typeof(SendGridClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        httpClientField.SetValue(client, new HttpClient(failureHandler));

        var result = await client.SendEmailAsync(CancellationToken.None);
        Assert.False(result.Status);

        var record = repository.LastSaved;
        Assert.NotNull(record);
        Assert.Equal(EmailProvider.SendGrid, record.Provider);
        Assert.False(string.IsNullOrWhiteSpace(record.MessageId));
        Assert.Equal(record.MessageId, result.MessageId);
        Assert.True(record.ProviderData.TryGetValue(SendGridPendingMessageSender.MessageJsonKey, out var json));
        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.True(record.ProviderData.TryGetValue(SendGridPendingMessageSender.ApiKeyProtectedKey, out var apiKeyProtected));
        Assert.False(string.IsNullOrWhiteSpace(apiKeyProtected));
        Assert.Equal("SG.API", CredentialProtection.UnprotectWithFallback(apiKeyProtected));
        Assert.DoesNotContain(SendGridPendingMessageSender.ApiKeyBase64Key, record.ProviderData.Keys);

        var successHandler = new TestHandler((request, _) => {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Bearer SG.API", request.Headers.Authorization?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        });
        var sender = new SendGridPendingMessageSender(new HttpClient(successHandler));
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.SendGrid, sender }
        });
        var processor = new PendingMessageProcessor(repository, factory);

        await processor.ProcessAsync(CancellationToken.None);

        Assert.Equal(1, successHandler.CallCount);
        Assert.Null(await repository.GetByMessageIdAsync(record!.MessageId, CancellationToken.None));
    }

    [Fact]
    public async Task SendGridClient_CallerCancellation_DoesNotQueuePendingMessage() {
        using var client = new SendGridClient {
            Credentials = new NetworkCredential("apikey", "SG.API"),
            From = "sender@example.com",
            To = new List<object> { "recipient@example.com" },
            Subject = "canceled",
            Text = "hello"
        };
        client.CreateMessage();

        var repository = new InMemoryPendingMessageRepository();
        client.PendingMessageRepository = repository;

        var delayedHandler = new DelayedCancellationHandler();
        var httpClientField = typeof(SendGridClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        httpClientField.SetValue(client, new HttpClient(delayedHandler));

        using var cts = new CancellationTokenSource();
        var sendTask = client.SendEmailAsync(cts.Token);
        await delayedHandler.WaitForStartAsync();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await sendTask);
        Assert.Null(repository.LastSaved);
    }

    [Fact]
    public async Task SendGridClient_CancellationDuringPendingSave_PropagatesCancellation() {
        using var client = new SendGridClient {
            Credentials = new NetworkCredential("apikey", "SG.API"),
            From = "sender@example.com",
            To = new List<object> { "recipient@example.com" },
            Subject = "pending-save-cancel",
            Text = "hello"
        };
        client.CreateMessage();

        var repository = new CancellationOnSavePendingMessageRepository();
        client.PendingMessageRepository = repository;

        var failureHandler = new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) {
            Content = new StringContent("failure", Encoding.UTF8, "text/plain")
        }));
        var httpClientField = typeof(SendGridClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        httpClientField.SetValue(client, new HttpClient(failureHandler));

        using var cts = new CancellationTokenSource();
        var sendTask = client.SendEmailAsync(cts.Token);
        await repository.WaitForSaveStartAsync();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await sendTask);
    }

    [Fact]
    public async Task MailgunClientQueuesAndProcessesPendingMessage() {
        var repository = new InMemoryPendingMessageRepository();
        using var client = new MailgunClient {
            PendingMessageRepository = repository,
            Credentials = new NetworkCredential("user", "mailgun-api-key"),
            From = "sender@example.com",
            To = new List<object> { "recipient@example.com" },
            Subject = "mailgun",
            Text = "body"
        };

        var failureHandler = new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway) {
            Content = new StringContent("error", Encoding.UTF8, "text/plain")
        }));
        var httpClientField = typeof(MailgunClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        httpClientField.SetValue(client, new HttpClient(failureHandler));

        var result = await client.SendEmailAsync(CancellationToken.None);
        Assert.False(result.Status);

        var record = repository.LastSaved;
        Assert.NotNull(record);
        Assert.Equal(EmailProvider.Mailgun, record.Provider);
        Assert.Equal(record.MessageId, result.MessageId);
        Assert.Equal("example.com", record.ProviderData[MailgunPendingMessageSender.DomainKey]);
        Assert.True(record.ProviderData.TryGetValue(MailgunPendingMessageSender.ApiKeyProtectedKey, out var mailgunProtected));
        Assert.False(string.IsNullOrWhiteSpace(mailgunProtected));
        Assert.Equal("mailgun-api-key", CredentialProtection.UnprotectWithFallback(mailgunProtected));
        Assert.DoesNotContain(MailgunPendingMessageSender.ApiKeyBase64Key, record.ProviderData.Keys);
        var mime = await MimeMessage.LoadAsync(new MemoryStream(Convert.FromBase64String(record.MimeMessage)));
        Assert.Equal("mailgun", mime.Subject);

        var successHandler = new TestHandler((request, _) => {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Contains("messages.mime", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("api:mailgun-api-key")), request.Headers.Authorization?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var sender = new MailgunPendingMessageSender(new HttpClient(successHandler));
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.Mailgun, sender }
        });
        var processor = new PendingMessageProcessor(repository, factory);

        await processor.ProcessAsync(CancellationToken.None);

        Assert.Equal(1, successHandler.CallCount);
        Assert.Null(await repository.GetByMessageIdAsync(record!.MessageId, CancellationToken.None));
    }

    [Fact]
    public async Task MailgunClient_HttpTimeoutQueuesPendingMessage() {
        var repository = new InMemoryPendingMessageRepository();
        using var client = new MailgunClient {
            PendingMessageRepository = repository,
            Credentials = new NetworkCredential("user", "mailgun-api-key"),
            From = "sender@example.com",
            To = new List<object> { "recipient@example.com" },
            Subject = "mailgun-timeout",
            Text = "body"
        };
        var timeoutHandler = new TestHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("HTTP request timeout")));
        var httpClientField = typeof(MailgunClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        httpClientField.SetValue(client, new HttpClient(timeoutHandler));

        var result = await client.SendEmailAsync(CancellationToken.None);

        Assert.False(result.Status);
        Assert.NotNull(repository.LastSaved);
        Assert.Equal(repository.LastSaved!.MessageId, result.MessageId);
        Assert.Equal(EmailProvider.Mailgun, repository.LastSaved.Provider);
    }

    [Fact]
    public async Task MailgunClient_CancellationDuringPendingSave_PropagatesCancellation() {
        var repository = new CancellationOnSavePendingMessageRepository();
        using var client = new MailgunClient {
            PendingMessageRepository = repository,
            Credentials = new NetworkCredential("user", "mailgun-api-key"),
            From = "sender@example.com",
            To = new List<object> { "recipient@example.com" },
            Subject = "mailgun-cancel",
            Text = "body"
        };

        var failureHandler = new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway) {
            Content = new StringContent("error", Encoding.UTF8, "text/plain")
        }));
        var httpClientField = typeof(MailgunClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        httpClientField.SetValue(client, new HttpClient(failureHandler));

        using var cts = new CancellationTokenSource();
        var sendTask = client.SendEmailAsync(cts.Token);
        await repository.WaitForSaveStartAsync();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await sendTask);
    }

    [Fact]
    public async Task GmailClientQueuesAndProcessesPendingMessage() {
        var credential = new OAuthCredential {
            UserName = "user@example.com",
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
            ClientId = "client-123",
            ClientSecret = "client-secret"
        };
        var repository = new InMemoryPendingMessageRepository();
        var failureHandler = new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) {
            Content = new StringContent("{\"error\":\"failed\"}", Encoding.UTF8, "application/json")
        }));
        using var failureClient = new HttpClient(failureHandler) {
            BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/")
        };
        using var client = new GmailApiClient(failureClient, credential: credential) {
            PendingMessageRepository = repository
        };

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.To.Add(MailboxAddress.Parse("recipient@example.com"));
        message.Subject = "gmail";
        message.Body = new TextPart("plain") { Text = "body" };

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync("me", message, CancellationToken.None));

        var record = repository.LastSaved;
        Assert.NotNull(record);
        Assert.Equal(EmailProvider.Gmail, record.Provider);
        Assert.Equal("me", record.ProviderData[GmailPendingMessageSender.UserIdKey]);
        Assert.Equal("user@example.com", record.ProviderData[GmailPendingMessageSender.UserNameKey]);
        var storedAccess = CredentialProtection.UnprotectWithFallback(record.ProviderData[GmailPendingMessageSender.AccessTokenProtectedKey]);
        Assert.Equal("access-token", storedAccess);
        var storedRefresh = CredentialProtection.UnprotectWithFallback(record.ProviderData[GmailPendingMessageSender.RefreshTokenProtectedKey]);
        Assert.Equal("refresh-token", storedRefresh);
        Assert.Equal("client-123", record.ProviderData[GmailPendingMessageSender.ClientIdKey]);
        var storedSecret = CredentialProtection.UnprotectWithFallback(record.ProviderData[GmailPendingMessageSender.ClientSecretProtectedKey]);
        Assert.Equal("client-secret", storedSecret);
        var queuedMessage = await MimeMessage.LoadAsync(new MemoryStream(Convert.FromBase64String(record.MimeMessage)));
        Assert.Equal("gmail", queuedMessage.Subject);

        var successHandler = new TestHandler((request, _) => {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Bearer access-token", request.Headers.Authorization?.ToString());
            Assert.Equal(new Uri("https://gmail.googleapis.com/gmail/v1/users/me/messages/send"), request.RequestUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{\"id\":\"msg\",\"threadId\":\"msg\"}", Encoding.UTF8, "application/json")
            });
        });
        var successClient = new HttpClient(successHandler) {
            BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/")
        };
        var sender = new GmailPendingMessageSender((c, refresher) => new GmailApiClient(successClient, refresher, credential: c));
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.Gmail, sender }
        });
        var processor = new PendingMessageProcessor(repository, factory);

        await processor.ProcessAsync(CancellationToken.None);

        Assert.Equal(1, successHandler.CallCount);
        Assert.Null(await repository.GetByMessageIdAsync(record!.MessageId, CancellationToken.None));
    }

    [Fact]
    public async Task GmailClient_CancellationDuringPendingSave_PropagatesCancellation() {
        var credential = new OAuthCredential {
            UserName = "user@example.com",
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
            ClientId = "client-123",
            ClientSecret = "client-secret"
        };
        var repository = new CancellationOnSavePendingMessageRepository();
        var failureHandler = new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) {
            Content = new StringContent("{\"error\":\"failed\"}", Encoding.UTF8, "application/json")
        }));
        using var failureClient = new HttpClient(failureHandler) {
            BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/")
        };
        using var client = new GmailApiClient(failureClient, credential: credential) {
            PendingMessageRepository = repository
        };

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.To.Add(MailboxAddress.Parse("recipient@example.com"));
        message.Subject = "gmail-cancel";
        message.Body = new TextPart("plain") { Text = "body" };

        using var cts = new CancellationTokenSource();
        var sendTask = client.SendAsync("me", message, cts.Token);
        await repository.WaitForSaveStartAsync();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await sendTask);
    }

    [Fact]
    public async Task GraphApplicationHandlerQueuesAndProcessesPendingMessage() {
        var repository = new InMemoryPendingMessageRepository();
        var handler = new Application.GraphMailSendHandler(
            new FakeGraphSessionFactory(),
            pendingMessageRepository: repository,
            sendAsync: (session, profile, request, message, cancellationToken) =>
                throw new HttpRequestException("temporary graph failure"));

        var queued = await handler.SendAsync(
            new Application.MailProfile {
                Id = "graph-profile",
                DisplayName = "Graph Profile",
                Kind = Application.MailProfileKind.Graph,
                DefaultMailbox = "shared@example.com",
                DefaultSender = "sender@example.com",
                Settings = new Dictionary<string, string> {
                    [Application.MailProfileSettingsKeys.TenantId] = "tenant-id"
                }
            },
            new Application.SendMessageRequest {
                ProfileId = "graph-profile",
                QueueOnFailure = true,
                Message = new Application.DraftMessage {
                    Subject = "graph-queued",
                    TextBody = "body",
                    To = {
                        new Application.MessageRecipient { Address = "recipient@example.com" }
                    }
                }
            },
            CancellationToken.None);

        Assert.True(queued.Queued);
        var record = repository.LastSaved;
        Assert.NotNull(record);
        Assert.Equal(EmailProvider.Graph, record!.Provider);
        Assert.Equal("shared@example.com", record.ProviderData[GraphPendingMessageSender.UserIdKey]);

        var successHandler = new TestHandler((request, _) => {
            if (request.RequestUri!.AbsoluteUri.EndsWith("/messages", StringComparison.OrdinalIgnoreCase)) {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) {
                    Content = new StringContent("{\"id\":\"draft-graph\"}", Encoding.UTF8, "application/json")
                });
            }

            if (request.RequestUri.AbsoluteUri.EndsWith("/send", StringComparison.OrdinalIgnoreCase)) {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted) {
                    Content = new StringContent(string.Empty, Encoding.UTF8, "text/plain")
                });
            }

            throw new InvalidOperationException("Unexpected Graph request: " + request.RequestUri);
        });
        using var graphHttpClient = new HttpClient(successHandler) {
            BaseAddress = new Uri("https://graph.microsoft.com/v1.0/")
        };
        var sender = new GraphPendingMessageSender(credential => new GraphApiClient(graphHttpClient, credential: credential));
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.Graph, sender }
        });
        var processor = new PendingMessageProcessor(repository, factory);

        await processor.ProcessAsync(CancellationToken.None);

        Assert.Equal(2, successHandler.CallCount);
        Assert.Null(await repository.GetByMessageIdAsync(record.MessageId, CancellationToken.None));
    }

    [Fact]
    public async Task SesClientQueuesAndProcessesPendingMessage() {
        var repository = new InMemoryPendingMessageRepository();
        using var client = new SesClient {
            PendingMessageRepository = repository,
            Credentials = new NetworkCredential("AKIA123", "secret-key"),
            Region = "us-east-1",
            From = "sender@example.com",
            To = new List<object> { "recipient@example.com" },
            Subject = "ses",
            Text = "body"
        };

        var failureHandler = new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) {
            Content = new StringContent("failure", Encoding.UTF8, "text/plain")
        }));
        var httpClientField = typeof(SesClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        httpClientField.SetValue(client, new HttpClient(failureHandler));

        var result = await client.SendEmailAsync(CancellationToken.None);
        Assert.False(result.Status);

        var record = repository.LastSaved;
        Assert.NotNull(record);
        Assert.Equal(EmailProvider.SES, record.Provider);
        Assert.Equal(record.MessageId, result.MessageId);
        Assert.True(record.ProviderData.TryGetValue(SesPendingMessageSender.AccessKeyIdProtectedKey, out var sesAccessProtected));
        Assert.True(record.ProviderData.TryGetValue(SesPendingMessageSender.SecretAccessKeyProtectedKey, out var sesSecretProtected));
        Assert.Equal("AKIA123", CredentialProtection.UnprotectWithFallback(sesAccessProtected));
        Assert.Equal("secret-key", CredentialProtection.UnprotectWithFallback(sesSecretProtected));
        Assert.DoesNotContain(SesPendingMessageSender.AccessKeyIdBase64Key, record.ProviderData.Keys);
        Assert.DoesNotContain(SesPendingMessageSender.SecretAccessKeyBase64Key, record.ProviderData.Keys);
        Assert.Equal("us-east-1", record.ProviderData[SesPendingMessageSender.RegionKey]);

        var successHandler = new TestHandler((request, _) => {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri("https://email.us-east-1.amazonaws.com/"), request.RequestUri);
            Assert.Equal("application/x-www-form-urlencoded", request.Content!.Headers.ContentType!.MediaType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var sender = new SesPendingMessageSender(new HttpClient(successHandler), () => new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
            { EmailProvider.SES, sender }
        });
        var processor = new PendingMessageProcessor(repository, factory);

        await processor.ProcessAsync(CancellationToken.None);

        Assert.Equal(1, successHandler.CallCount);
        Assert.Null(await repository.GetByMessageIdAsync(record!.MessageId, CancellationToken.None));
    }

    [Fact]
    public async Task SesClient_HttpTimeoutQueuesPendingMessage() {
        var repository = new InMemoryPendingMessageRepository();
        using var client = new SesClient {
            PendingMessageRepository = repository,
            Credentials = new NetworkCredential("AKIA123", "secret-key"),
            Region = "us-east-1",
            From = "sender@example.com",
            To = new List<object> { "recipient@example.com" },
            Subject = "ses-timeout",
            Text = "body"
        };
        var timeoutHandler = new TestHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("HTTP request timeout")));
        var httpClientField = typeof(SesClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        httpClientField.SetValue(client, new HttpClient(timeoutHandler));

        var result = await client.SendEmailAsync(CancellationToken.None);

        Assert.False(result.Status);
        Assert.NotNull(repository.LastSaved);
        Assert.Equal(repository.LastSaved!.MessageId, result.MessageId);
        Assert.Equal(EmailProvider.SES, repository.LastSaved.Provider);
    }

    [Fact]
    public async Task SesClient_CancellationDuringPendingSave_PropagatesCancellation() {
        var repository = new CancellationOnSavePendingMessageRepository();
        using var client = new SesClient {
            PendingMessageRepository = repository,
            Credentials = new NetworkCredential("AKIA123", "secret-key"),
            Region = "us-east-1",
            From = "sender@example.com",
            To = new List<object> { "recipient@example.com" },
            Subject = "ses-cancel",
            Text = "body"
        };

        var failureHandler = new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) {
            Content = new StringContent("failure", Encoding.UTF8, "text/plain")
        }));
        var httpClientField = typeof(SesClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        httpClientField.SetValue(client, new HttpClient(failureHandler));

        using var cts = new CancellationTokenSource();
        var sendTask = client.SendEmailAsync(cts.Token);
        await repository.WaitForSaveStartAsync();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await sendTask);
    }

    private sealed class FakeGraphSessionFactory : Application.IGraphSessionFactory {
        public Task<Application.GraphSession> ConnectAsync(Application.MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Application.GraphSession(
                new GraphApiClient(new OAuthCredential {
                    UserName = profile.DefaultMailbox ?? "me",
                    AccessToken = "graph-token",
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
                }),
                profile.DefaultMailbox ?? "me",
                new OAuthCredential {
                    UserName = profile.DefaultMailbox ?? "me",
                    AccessToken = "graph-token",
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
                },
                new GraphCredential {
                    ClientId = "client-id",
                    DirectoryId = "tenant-id",
                    ClientSecret = "client-secret"
                }));
    }
}
