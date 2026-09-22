namespace Mailozaurr.Tests;

public sealed class FilePendingMessageCoordinationTests {
    [Fact]
    public async Task SeparateRepositoriesObserveWritesAndDoNotAcquireTheSameLease() {
        var directory = Path.Combine(Path.GetTempPath(), "mailozaurr-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "pending.log");
        try {
            var first = new FilePendingMessageRepository(path);
            var second = new FilePendingMessageRepository(path);
            var now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
            await first.SaveAsync(new PendingMessageRecord {
                MessageId = "shared-message",
                Timestamp = now,
                NextAttemptAt = now
            });

            Assert.NotNull(await second.GetByMessageIdAsync("shared-message"));
            var leases = await Task.WhenAll(
                first.TryAcquireLeaseAsync("shared-message", now, now.AddMinutes(1)),
                second.TryAcquireLeaseAsync("shared-message", now, now.AddMinutes(1)));
            Assert.Single(leases, lease => lease != null);

            await second.RemoveAsync("shared-message");
            Assert.Null(await first.GetByMessageIdAsync("shared-message"));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ForcedRetryRespectsAnExistingLeaseAndRenewalUsesCompareAndSwap() {
        var directory = Path.Combine(Path.GetTempPath(), "mailozaurr-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var path = Path.Combine(directory, "pending.log");
            var first = new FilePendingMessageRepository(path);
            var second = new FilePendingMessageRepository(path);
            var now = DateTimeOffset.UtcNow;
            await first.SaveAsync(new PendingMessageRecord {
                MessageId = "scheduled-retry",
                Timestamp = now,
                NextAttemptAt = now.AddHours(1)
            });

            var initialLease = now.AddMinutes(1);
            var lease = await first.TryAcquireForcedLeaseAsync("scheduled-retry", now, initialLease);
            Assert.NotNull(lease);
            Assert.Null(await second.TryAcquireForcedLeaseAsync("scheduled-retry", now, now.AddMinutes(2)));

            var renewedLease = now.AddMinutes(3);
            Assert.True(await second.TryRenewLeaseAsync("scheduled-retry", lease!.ProcessingLeaseId!, initialLease, renewedLease));
            Assert.False(await first.TryRenewLeaseAsync("scheduled-retry", lease.ProcessingLeaseId!, initialLease, now.AddMinutes(4)));
            Assert.Null(await first.TryAcquireLeaseAsync("scheduled-retry", now.AddMinutes(2), now.AddMinutes(4)));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LongProviderSendKeepsTheLeasePastItsInitialExpiry() {
        var directory = Path.Combine(Path.GetTempPath(), "mailozaurr-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var path = Path.Combine(directory, "pending.log");
            var first = new FilePendingMessageRepository(path);
            var second = new FilePendingMessageRepository(path);
            var now = DateTimeOffset.UtcNow;
            await first.SaveAsync(new PendingMessageRecord {
                MessageId = "long-send",
                Timestamp = now,
                NextAttemptAt = now
            });
            var sender = new BlockingSender();
            var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
                [EmailProvider.None] = sender
            });
            var duration = TimeSpan.FromMilliseconds(450);
            var firstProcessor = new PendingMessageProcessor(first, factory,
                processingLeaseDuration: duration);
            var firstRun = firstProcessor.ProcessAsync();
            await sender.Started;
            try {
                var initialLease = (await second.GetByMessageIdAsync("long-send"))!.ProcessingLeaseUntil!.Value;
                var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
                while (DateTimeOffset.UtcNow <= initialLease ||
                       (await second.GetByMessageIdAsync("long-send"))?.ProcessingLeaseUntil <= initialLease) {
                    if (DateTimeOffset.UtcNow > deadline) throw new TimeoutException("The send lease was not renewed.");
                    await Task.Delay(25);
                }

                var secondProcessor = new PendingMessageProcessor(second, factory,
                    processingLeaseDuration: duration);
                await secondProcessor.ProcessAsync();
                Assert.Equal(1, sender.SendCount);
            } finally {
                sender.Release();
                await firstRun;
            }
            Assert.Null(await second.GetByMessageIdAsync("long-send"));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task WorkerThatLosesLeaseCannotAcknowledgeAnUncancelableSend() {
        var directory = Path.Combine(Path.GetTempPath(), "mailozaurr-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var path = Path.Combine(directory, "pending.log");
            var first = new FilePendingMessageRepository(path);
            var second = new FilePendingMessageRepository(path);
            var now = DateTimeOffset.UtcNow;
            await first.SaveAsync(new PendingMessageRecord {
                MessageId = "lease-lost",
                Timestamp = now,
                NextAttemptAt = now
            });
            var sender = new BlockingSender();
            var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {
                [EmailProvider.None] = sender
            });
            var run = new PendingMessageProcessor(first, factory,
                processingLeaseDuration: TimeSpan.FromMilliseconds(300)).ProcessAsync();
            await sender.Started;
            try {
                var replacement = (await second.GetByMessageIdAsync("lease-lost"))!;
                replacement.ProcessingLeaseId = Guid.NewGuid().ToString("N");
                replacement.ProcessingLeaseUntil = now.AddMinutes(5);
                replacement.NextAttemptAt = replacement.ProcessingLeaseUntil.Value;
                await second.SaveAsync(replacement);
                await Task.Delay(450);
            } finally {
                sender.Release();
            }

            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => run);
            var remaining = await second.GetByMessageIdAsync("lease-lost");
            Assert.NotNull(remaining);
            Assert.Null(remaining!.DeliveryAcceptedAt);
            Assert.Equal(1, sender.SendCount);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class BlockingSender : IPendingMessageSender {
        private readonly TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => started.Task;
        public int SendCount { get; private set; }

        public async Task SendAsync(PendingMessageRecord record, CancellationToken cancellationToken) {
            SendCount++;
            started.TrySetResult(true);
            await release.Task;
        }

        public void Release() => release.TrySetResult(true);
    }
}
