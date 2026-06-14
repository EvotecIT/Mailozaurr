using MailKit;
using MailKit.Security;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests.SentMessages;

public sealed class SmtpPendingMessageProcessorIntegrationTests {
    [Fact]
    public async Task QueuedRecordReplaysStoredSecuritySettingsAsync() {
        var repository = new RecordingPendingMessageRepository();
        var originalFactory = Smtp.ClientFactory;
        var failingClient = new FailingClient();
        Smtp.ClientFactory = _ => failingClient;

        Smtp? smtp = null;
        try {
            smtp = new Smtp();
            smtp.PendingMessageRepository = repository;
            smtp.SkipCertificateValidation = true;
            smtp.CheckCertificateRevocation = false;
            smtp.Timeout = 12345;

            await smtp.ConnectAsync("smtp.integration.test", 587, SecureSocketOptions.Auto, useSsl: true);

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse("sender@example.com"));
            message.To.Add(MailboxAddress.Parse("recipient@example.com"));
            message.Subject = "queued";
            message.Body = new TextPart("plain") { Text = "body" };
            message.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId();
            smtp.Message = message;

            var sendResult = await smtp.SendAsync(CancellationToken.None);
            Assert.False(sendResult.Status);

            var record = Assert.Single(repository.Records);
            Assert.Equal("smtp.integration.test", record.Server);
            Assert.Equal(587, record.Port);
            Assert.Equal(SecureSocketOptions.StartTls.ToString(), record.ProviderData["SecureSocketOptions"]);
            Assert.Equal(bool.TrueString, record.ProviderData["UseSsl"]);
            Assert.Equal(bool.TrueString, record.ProviderData["SkipCertificateValidation"]);
            Assert.Equal(bool.FalseString, record.ProviderData["CheckCertificateRevocation"]);
            Assert.Equal("12345", record.ProviderData["TimeoutMilliseconds"]);

            var captureClient = new CapturingClient();
            var sender = new SmtpPendingMessageSender(
                () => captureClient,
                secureSocketOptions: SecureSocketOptions.SslOnConnect,
                useSsl: false,
                skipCertificateValidation: false,
                checkCertificateRevocation: true,
                timeout: 9876);

            var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
                { EmailProvider.None, sender }
            });

            var processor = new PendingMessageProcessor(
                repository,
                factory,
                clock: () => DateTimeOffset.UtcNow.AddMinutes(1),
                processingLeaseDuration: TimeSpan.Zero);

            await processor.ProcessAsync();

            Assert.Equal("smtp.integration.test", captureClient.Host);
            Assert.Equal(587, captureClient.Port);
            Assert.Equal(SecureSocketOptions.StartTls, captureClient.Options);
            Assert.Equal(12345, captureClient.Timeout);
            Assert.False(captureClient.CheckCertificateRevocation);

            var callback = captureClient.ServerCertificateValidationCallback;
            Assert.NotNull(callback);
            Assert.True(callback!(new object(), null!, null!, SslPolicyErrors.None));

            Assert.Empty(repository.Records);
        } finally {
            smtp?.Dispose();
            Smtp.ClientFactory = originalFactory;
        }
    }

    private sealed class FailingClient : ClientSmtp {
        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null) =>
            throw new InvalidOperationException("Simulated failure");
    }

    private sealed class CapturingClient : ClientSmtp {
        public string? Host { get; private set; }
        public int Port { get; private set; }
        public SecureSocketOptions Options { get; private set; }

        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {
            Host = host;
            Port = port;
            Options = options;
            return Task.CompletedTask;
        }

        public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null) =>
            Task.FromResult(message.MessageId ?? string.Empty);
    }

    private sealed class RecordingPendingMessageRepository : IPendingMessageRepository {
        private readonly List<PendingMessageRecord> records = new();

        public IReadOnlyList<PendingMessageRecord> Records => records;

        public Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) {
            records.RemoveAll(r => string.Equals(r.MessageId, record.MessageId, StringComparison.Ordinal));
            records.Add(record);
            return Task.CompletedTask;
        }

        public Task<PendingMessageRecord?> TryAcquireLeaseAsync(
            string messageId,
            DateTimeOffset dueBeforeOrAt,
            DateTimeOffset leaseUntil,
            CancellationToken cancellationToken = default) {
            var record = records.FirstOrDefault(r => string.Equals(r.MessageId, messageId, StringComparison.Ordinal));
            if (record == null || record.NextAttemptAt > dueBeforeOrAt) {
                return Task.FromResult<PendingMessageRecord?>(null);
            }

            record.NextAttemptAt = leaseUntil;
            return Task.FromResult<PendingMessageRecord?>(record);
        }

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) {
            var record = records.FirstOrDefault(r => string.Equals(r.MessageId, messageId, StringComparison.Ordinal));
            return Task.FromResult<PendingMessageRecord?>(record);
        }

        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default) {
            foreach (var record in records.ToList()) {
                cancellationToken.ThrowIfCancellationRequested();
                yield return record;
                await Task.Yield();
            }
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
            records.RemoveAll(r => string.Equals(r.MessageId, messageId, StringComparison.Ordinal));
            return Task.CompletedTask;
        }
    }
}