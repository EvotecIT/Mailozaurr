using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MimeKit;
using Xunit;

namespace Mailozaurr.Tests.SentMessages;

public sealed class SmtpPendingMessageEncryptionTests {
    [Fact]
    public async Task QueuedRecordsRoundTripCredentialsOnNonWindowsAsync() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return;
        }

        var repository = new RecordingPendingMessageRepository();
        var originalFactory = Smtp.ClientFactory;
        var failingClient = new ThrowingClientSmtp();
        Smtp.ClientFactory = _ => failingClient;

        Smtp? smtp = null;
        try {
            smtp = new Smtp();
            smtp.PendingMessageRepository = repository;

            var credentialSetter = typeof(Smtp).GetProperty(nameof(Smtp.Credential))?.GetSetMethod(true);
            credentialSetter?.Invoke(smtp, new object[] { new NetworkCredential("queued-user", "SuperSecret!42") });

            var serverSetter = typeof(Smtp).GetProperty(nameof(Smtp.Server))?.GetSetMethod(true);
            serverSetter?.Invoke(smtp, new object[] { "smtp.example.com" });

            var portSetter = typeof(Smtp).GetProperty(nameof(Smtp.Port))?.GetSetMethod(true);
            portSetter?.Invoke(smtp, new object[] { 2525 });

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse("sender@example.com"));
            message.To.Add(MailboxAddress.Parse("recipient@example.com"));
            message.Subject = "queued";
            message.Body = new TextPart("plain") { Text = "body" };
            message.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId();
            smtp.Message = message;

            var result = await smtp.SendAsync(CancellationToken.None);
            Assert.False(result.Status);

            var record = Assert.Single(repository.Records);
            Assert.Equal("queued-user", record.UserName);
            Assert.NotNull(record.Password);

            var plainBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("SuperSecret!42"));
            Assert.NotEqual(plainBase64, record.Password);

            var decrypted = CredentialProtection.Default.Unprotect(record.Password!);
            Assert.Equal("SuperSecret!42", decrypted);

            var recordingProtector = new RecordingProtector(CredentialProtection.Default);
            var sender = new SmtpPendingMessageSender(() => new RecordingClient(), credentialProtector: recordingProtector);
            var decoded = sender.DecodePassword(record.Password);

            Assert.Equal("SuperSecret!42", decoded);
            Assert.Equal("SuperSecret!42", recordingProtector.LastUnprotected);
        } finally {
            smtp?.Dispose();
            Smtp.ClientFactory = originalFactory;
        }
    }

    private sealed class ThrowingClientSmtp : ClientSmtp {
        public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null) =>
            throw new InvalidOperationException("Simulated failure");
    }

    private sealed class RecordingClient : ClientSmtp { }

    private sealed class RecordingProtector : ICredentialProtector {
        private readonly ICredentialProtector inner;

        public RecordingProtector(ICredentialProtector inner) {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public string? LastUnprotected { get; private set; }

        public string Protect(string plainText) => inner.Protect(plainText);

        public string Unprotect(string protectedData) {
            var value = inner.Unprotect(protectedData);
            LastUnprotected = value;
            return value;
        }
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
