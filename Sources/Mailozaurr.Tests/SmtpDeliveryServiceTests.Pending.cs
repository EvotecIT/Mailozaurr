using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public partial class SmtpDeliveryServiceTests {
    private sealed class MemoryPendingRepository : IPendingMessageRepository {
        internal PendingMessageRecord? Record;
        internal bool FailEnumeration;
        internal bool Removed;

        public Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) {
            Record = record;
            return Task.CompletedTask;
        }

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) => Task.FromResult(Record);

        public Task<PendingMessageRecord?> TryAcquireLeaseAsync(string messageId, DateTimeOffset dueBeforeOrAt, DateTimeOffset leaseUntil, CancellationToken cancellationToken = default) {
            if (Record is null || Record.NextAttemptAt > dueBeforeOrAt) {
                return Task.FromResult<PendingMessageRecord?>(null);
            }
            Record.NextAttemptAt = leaseUntil;
            return Task.FromResult<PendingMessageRecord?>(Record);
        }

        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default) {
            await Task.Yield();
            if (FailEnumeration) throw new IOException(PrivateFailure);
            if (Record is { } record) yield return record;
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
            Removed = true;
            Record = null;
            return Task.CompletedTask;
        }
    }

    private static async Task<MemoryPendingRepository> CreatePendingRepositoryAsync(Smtp smtp) {
        await smtp.CreateMessageAsync();
        using var stream = new MemoryStream();
        await smtp.Message.WriteToAsync(stream);
        return new MemoryPendingRepository {
            Record = new PendingMessageRecord {
                MessageId = "queued-message",
                MimeMessage = Convert.ToBase64String(stream.ToArray()),
                Server = "smtp.example.test",
                Port = 587,
                Provider = EmailProvider.None,
                NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            }
        };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessPendingMessagesAsync_UsesConfiguredInMemoryTransport_AndPreservesRetrySemantics(bool failSend) {
        var client = new MemoryClient { FailAt = failSend ? "send" : null, FailDisconnect = true };
        var smtp = CreateSmtp(client);
        var repository = await CreatePendingRepositoryAsync(smtp);
        smtp.PendingMessageRepository = repository;
        smtp.LogCollector = new LogCollector();
        using var trace = new StringWriter();
        using var listener = new TextWriterTraceListener(trace);
        Trace.Listeners.Add(listener);
        try {
            await SmtpDeliveryService.ProcessPendingMessagesAsync(smtp);
            Assert.Equal(new[] { "connect", "send", "disconnect" }, client.Calls);
            Assert.Equal(!failSend, repository.Removed);
            if (failSend) {
                Assert.NotNull(repository.Record);
                Assert.Equal(1, repository.Record!.AttemptCount);
            }
            Assert.Empty(smtp.LogCollector.Logs);
            Assert.True(client.Disposed);
            listener.Flush();
            Assert.Contains("SMTP disconnect failed.", trace.ToString());
            Assert.DoesNotContain(PrivateFailure, trace.ToString());
        } finally {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public async Task ProcessPendingMessagesAsync_RepositoryFailure_IsSanitizedAndDisposes() {
        var client = new MemoryClient();
        var smtp = CreateSmtp(client);
        smtp.PendingMessageRepository = new MemoryPendingRepository { FailEnumeration = true };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SmtpDeliveryService.ProcessPendingMessagesAsync(smtp));
        Assert.Equal("SMTP pending-message processing failed.", error.Message);
        Assert.Null(error.InnerException);
        Assert.True(client.Disposed);
    }

    [Fact]
    public async Task ProcessPendingMessagesAsync_CanceledSend_IsSanitizedAndDoesNotConsumeRetry() {
        using var cancellation = new CancellationTokenSource();
        var client = new MemoryClient { CancelAt = "send", Cancellation = cancellation };
        var smtp = CreateSmtp(client);
        var repository = await CreatePendingRepositoryAsync(smtp);
        smtp.PendingMessageRepository = repository;
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SmtpDeliveryService.ProcessPendingMessagesAsync(smtp, cancellation.Token));
        Assert.Equal("SMTP operation canceled.", error.Message);
        Assert.Equal(client.Tokens[client.Tokens.Count - 1], error.CancellationToken);
        Assert.Null(error.InnerException);
        Assert.False(repository.Removed);
        Assert.Equal(0, repository.Record!.AttemptCount);
        Assert.True(client.Disposed);
    }
}
