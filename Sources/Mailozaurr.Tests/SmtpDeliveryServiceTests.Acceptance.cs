using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public partial class SmtpDeliveryServiceTests {
    private sealed class CancelingSentRepository : ISentMessageRepository {
        private readonly CancellationTokenSource _cancellation;
        private readonly bool _fail;
        internal bool Saved;

        internal CancelingSentRepository(CancellationTokenSource cancellation, bool fail) {
            _cancellation = cancellation;
            _fail = fail;
        }

        public Task SaveAsync(SentMessageRecord record, CancellationToken cancellationToken = default) {
            Saved = true;
            _cancellation.Cancel();
            return _fail ? Task.FromException(new IOException(PrivateFailure)) : Task.CompletedTask;
        }

        public Task<SentMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default)
            => Task.FromResult<SentMessageRecord?>(null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendAsync_CancellationDuringAcceptedBookkeeping_PreservesSuccessfulDelivery(bool failReceipt) {
        using var cancellation = new CancellationTokenSource();
        var client = new MemoryClient();
        var smtp = CreateSmtp(client);
        var repository = new CancelingSentRepository(cancellation, failReceipt);
        smtp.SentMessageRepository = repository;

        await SmtpDeliveryService.SendAsync(smtp, Request(), cancellation.Token);

        Assert.True(repository.Saved);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Single(client.Calls.FindAll(call => call == "send"));
        Assert.NotNull(client.SentMessage);
        Assert.True(client.Disposed);
    }
}
