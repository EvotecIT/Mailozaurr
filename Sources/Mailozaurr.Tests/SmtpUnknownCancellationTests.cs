namespace Mailozaurr.Tests;

public sealed class SmtpUnknownCancellationTests {
    [Fact]
    public async Task UnknownOutcomeCommittedDuringRenewalPreservesCallerCancellationAndHold() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr-submission", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var repository = new ReleaseRaceRepository(Path.Combine(directory, "pending.jsonl"));
            await repository.SaveAsync(new PendingMessageRecord { MessageId = "cancelled-unknown", Timestamp = DateTimeOffset.UtcNow });
            using var caller = new CancellationTokenSource();
            var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
                [EmailProvider.None] = new UnknownSender(repository, caller)
            });
            var logger = new InternalLogger();
            var warned = false;
            logger.OnWarningMessage += (_, args) => {
                if (args.Message.Contains("is unknown")) {
                    warned = true;
                    throw new InvalidOperationException("Warning subscriber failed");
                }
            };
            var run = new PendingMessageProcessor(repository, factory, logger: logger, processingLeaseDuration: TimeSpan.FromSeconds(3)).ProcessAsync(caller.Token);
            try {
                Assert.Same(run, await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10))));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
                var held = await repository.GetByMessageIdAsync("cancelled-unknown");
                Assert.NotNull(held?.DeliveryUnknownAt);
                Assert.Null(held!.ProcessingLeaseId);
                Assert.Equal(1, held.AttemptCount);
                Assert.True(repository.RenewalObservedRelease);
                Assert.True(warned);
            } finally {
                caller.Cancel();
                repository.Released.TrySetResult(true);
                try { await run; } catch (OperationCanceledException) { }
            }
        } finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class UnknownSender(ReleaseRaceRepository repository, CancellationTokenSource caller) : IPendingMessageSender {
        public async Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
            await repository.Renewing.Task;
            caller.Cancel();
            throw new SmtpDeliveryUnknownException(new OperationCanceledException(ct));
        }
    }

    /// <summary>Real file commits, with a renewal deliberately completing after the fenced release.</summary>
    private sealed class ReleaseRaceRepository(string path) : IPendingMessageRepository, IPendingMessageLeaseRenewer, IPendingMessageLeaseCommitter {
        private readonly FilePendingMessageRepository inner = new(path);
        internal TaskCompletionSource<bool> Renewing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool RenewalObservedRelease;
        public Task SaveAsync(PendingMessageRecord record, CancellationToken ct = default) => inner.SaveAsync(record, ct);
        public Task<PendingMessageRecord?> GetByMessageIdAsync(string id, CancellationToken ct = default) => inner.GetByMessageIdAsync(id, ct);
        public Task<PendingMessageRecord?> TryAcquireLeaseAsync(string id, DateTimeOffset due, DateTimeOffset until, CancellationToken ct = default) => inner.TryAcquireLeaseAsync(id, due, until, ct);
        public IAsyncEnumerable<PendingMessageRecord> GetAllAsync(CancellationToken ct = default) => inner.GetAllAsync(ct);
        public Task RemoveAsync(string id, CancellationToken ct = default) => inner.RemoveAsync(id, ct);
        public Task<bool> TryRemoveWithLeaseAsync(string id, string lease, CancellationToken ct = default) => inner.TryRemoveWithLeaseAsync(id, lease, ct);
        public async Task<bool> TrySaveWithLeaseAsync(PendingMessageRecord record, string lease, CancellationToken ct = default) {
            var saved = await inner.TrySaveWithLeaseAsync(record, lease, ct);
            if (saved && record.DeliveryUnknownAt.HasValue && record.ProcessingLeaseId == null) Released.TrySetResult(true);
            return saved;
        }
        public async Task<bool> TryRenewLeaseAsync(string id, string lease, DateTimeOffset expected, DateTimeOffset next, CancellationToken ct = default) {
            Renewing.TrySetResult(true);
            await Released.Task;
            var renewed = await inner.TryRenewLeaseAsync(id, lease, expected, next, CancellationToken.None);
            RenewalObservedRelease = !renewed;
            return renewed;
        }
    }
}
