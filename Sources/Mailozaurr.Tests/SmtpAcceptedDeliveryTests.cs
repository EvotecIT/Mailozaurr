using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using MailKit;
using MimeKit;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public sealed class SmtpAcceptedDeliveryTests {
    [Fact]
    public async Task QueueRemovalFailure_PersistsAcceptanceAndWorkerDoesNotResend() {
        var client = new AcceptedClient();
        var smtp = await CreateAsync(client);
        var repository = new RemovalFailureRepository(new PendingMessageRecord {
            MessageId = smtp.Message.MessageId!, NextAttemptAt = DateTimeOffset.UtcNow
        });
        smtp.PendingMessageRepository = repository;
        smtp.RetryCount = 2;
        smtp.RetryAlways = true;
        var result = await smtp.SendAsync();
        Assert.True(result.DeliveryAccepted);
        Assert.True(result.Status);
        Assert.Contains("Queue acknowledgement", Assert.Single(result.PostSendErrors));
        Assert.NotNull(repository.Record.DeliveryAcceptedAt);
        var sender = new UnexpectedSender();
        var factory = new PendingMessageSenderFactory(new[] {
            new KeyValuePair<EmailProvider, IPendingMessageSender>(EmailProvider.None, sender)
        });
        await Assert.ThrowsAsync<IOException>(() => new PendingMessageProcessor(repository, factory).ProcessAsync());
        Assert.Equal(1, client.Sends);
        Assert.Equal(0, sender.Sends);
        smtp.Dispose();
    }

    [Fact]
    public async Task WebhookHttpFailure_IsReportedWithoutChangingAcceptanceOrRetrying() {
        var originalHttp = Helpers.SharedHttpClient;
        using var http = new HttpClient(new FailedWebhookHandler());
        Helpers.SharedHttpClient = http;
        try {
            var client = new AcceptedClient();
            var smtp = await CreateAsync(client);
            smtp.WebhookUrl = "https://webhook.example.test/accepted";
            smtp.RetryCount = 2;
            smtp.RetryAlways = true;
            var result = await smtp.SendAsync();
            Assert.True(result.DeliveryAccepted);
            Assert.True(result.Status);
            Assert.Equal(1, client.Sends);
            Assert.Contains("Webhook notification", Assert.Single(result.PostSendErrors));
            smtp.Dispose();
        } finally {
            Helpers.SharedHttpClient = originalHttp;
        }
    }

    private static async Task<Smtp> CreateAsync(ClientSmtp client) {
        var smtp = new Smtp();
        typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(smtp, client);
        smtp.From = "sender@example.test";
        smtp.To = new object[] { "recipient@example.test" };
        smtp.TextBody = "body";
        await smtp.CreateMessageAsync();
        return smtp;
    }

    private sealed class AcceptedClient : ClientSmtp {
        internal int Sends;
        public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null) {
            Sends++;
            return Task.FromResult("accepted");
        }
    }

    private sealed class FailedWebhookHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }

    private sealed class UnexpectedSender : IPendingMessageSender {
        internal int Sends;
        public Task SendAsync(PendingMessageRecord record, CancellationToken ct) { Sends++; return Task.CompletedTask; }
    }

    private sealed class RemovalFailureRepository(PendingMessageRecord record) : IPendingMessageRepository {
        internal PendingMessageRecord Record = record;
        public Task SaveAsync(PendingMessageRecord value, CancellationToken cancellationToken = default) { Record = value.Clone(); return Task.CompletedTask; }
        public Task<PendingMessageRecord?> GetByMessageIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<PendingMessageRecord?>(Record.Clone());
        public Task<PendingMessageRecord?> TryAcquireLeaseAsync(string id, DateTimeOffset due, DateTimeOffset until, CancellationToken cancellationToken = default) => Task.FromResult<PendingMessageRecord?>(Record.Clone());
        public Task RemoveAsync(string id, CancellationToken cancellationToken = default) => Task.FromException(new IOException("queue removal unavailable"));
        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default) {
            await Task.CompletedTask;
            yield return Record.Clone();
        }
    }
}
