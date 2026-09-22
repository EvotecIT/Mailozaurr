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
    public async Task ForcedRetryConservativelyHonorsLegacyFutureLeaseAfterCompaction() {
        var directory = Path.Combine(Path.GetTempPath(), "mailozaurr-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var path = Path.Combine(directory, "pending.log");
            var now = DateTimeOffset.UtcNow;
            var legacy = new PendingMessageLogEnvelope {
                EntryType = "upsert",
                MessageId = "legacy-in-flight",
                Record = new PendingMessageRecord {
                    MessageId = "legacy-in-flight",
                    Timestamp = now,
                    NextAttemptAt = now.AddMinutes(5)
                }
            };
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(legacy) + Environment.NewLine);
            var repository = new FilePendingMessageRepository(path);

            for (var index = 0; index < 40; index++) {
                var id = "churn-" + index;
                await repository.SaveAsync(new PendingMessageRecord {
                    MessageId = id, Timestamp = now, NextAttemptAt = now
                });
                var lease = await repository.TryAcquireLeaseAsync(id, now, now.AddMinutes(1));
                Assert.NotNull(lease);
                Assert.True(await repository.TryRemoveWithLeaseAsync(id, lease!.ProcessingLeaseId!));
            }

            Assert.Null(await repository.TryAcquireForcedLeaseAsync(
                "legacy-in-flight", now, now.AddMinutes(2)));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CompactionFailureDoesNotChangeCommittedMutationResult() {
        var directory = Path.Combine(Path.GetTempPath(), "mailozaurr-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var path = Path.Combine(directory, "pending.log");
            var repository = new FilePendingMessageRepository(path,
                _ => Task.FromException(new IOException("Simulated compaction failure.")));
            var now = DateTimeOffset.UtcNow;

            await repository.SaveAsync(new PendingMessageRecord {
                MessageId = "committed", Timestamp = now, NextAttemptAt = now
            });
            var lease = await repository.TryAcquireLeaseAsync("committed", now, now.AddMinutes(1));

            Assert.NotNull(lease);
            Assert.Equal(lease!.ProcessingLeaseId,
                (await repository.GetByMessageIdAsync("committed"))!.ProcessingLeaseId);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ConcurrentMutationsDoNotRunOverlappingCompactions() {
        var directory = Path.Combine(Path.GetTempPath(), "mailozaurr-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var active = 0;
            var maxActive = 0;
            var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var repository = new FilePendingMessageRepository(Path.Combine(directory, "pending.log"), async _ => {
                var current = Interlocked.Increment(ref active);
                var observed = Volatile.Read(ref maxActive);
                while (current > observed && Interlocked.CompareExchange(ref maxActive, current, observed) != observed)
                    observed = Volatile.Read(ref maxActive);
                if (!firstStarted.Task.IsCompleted) {
                    firstStarted.TrySetResult(true);
                    await releaseFirst.Task;
                }
                Interlocked.Decrement(ref active);
            });
            var now = DateTimeOffset.UtcNow;
            var first = repository.SaveAsync(new PendingMessageRecord {
                MessageId = "first", Timestamp = now, NextAttemptAt = now
            });
            await firstStarted.Task;
            var second = repository.SaveAsync(new PendingMessageRecord {
                MessageId = "second", Timestamp = now, NextAttemptAt = now
            });
            await Task.Delay(100);
            releaseFirst.TrySetResult(true);
            await Task.WhenAll(first, second);

            Assert.Equal(1, maxActive);
            Assert.NotNull(await repository.GetByMessageIdAsync("first"));
            Assert.NotNull(await repository.GetByMessageIdAsync("second"));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AcceptanceRetainsRenewedLeaseUntilCleanup() {
        var directory = Path.Combine(Path.GetTempPath(), "mailozaurr-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var repository = new FilePendingMessageRepository(Path.Combine(directory, "pending.log"));
            var now = DateTimeOffset.UtcNow;
            await repository.SaveAsync(new PendingMessageRecord {
                MessageId = "renewed-send", Timestamp = now, NextAttemptAt = now
            });
            var initial = now.AddSeconds(1);
            var lease = await repository.TryAcquireLeaseAsync("renewed-send", now, initial);
            Assert.NotNull(lease);
            var renewed = now.AddMinutes(2);
            Assert.True(await repository.TryRenewLeaseAsync("renewed-send",
                lease!.ProcessingLeaseId!, initial, renewed));

            var accepted = lease.Clone();
            accepted.DeliveryAcceptedAt = now.AddSeconds(2);
            accepted.NextAttemptAt = accepted.DeliveryAcceptedAt.Value;
            Assert.True(await repository.TrySaveWithLeaseAsync(accepted, lease.ProcessingLeaseId!));

            var persisted = await repository.GetByMessageIdAsync("renewed-send");
            Assert.Equal(renewed, persisted!.ProcessingLeaseUntil);
            Assert.Equal(renewed, persisted.NextAttemptAt);
            Assert.Null(await repository.TryAcquireLeaseAsync("renewed-send",
                now.AddSeconds(3), now.AddMinutes(3)));
            Assert.True(await repository.TryRemoveWithLeaseAsync("renewed-send", lease.ProcessingLeaseId!));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LeaseOnlyDrainCompactsAndRemovesOldAbandonedCompactionFiles() {
        var directory = Path.Combine(Path.GetTempPath(), "mailozaurr-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var path = Path.Combine(directory, "pending.log");
            var oldTemp = path + ".compact." + Guid.NewGuid().ToString("N");
            var legacyTemp = path + ".compact";
            var recentTemp = path + ".compact." + Guid.NewGuid().ToString("N");
            File.WriteAllText(oldTemp, "abandoned MIME");
            File.SetLastWriteTimeUtc(oldTemp, DateTime.UtcNow.AddDays(-2));
            File.WriteAllText(legacyTemp, "abandoned legacy MIME");
            File.SetLastWriteTimeUtc(legacyTemp, DateTime.UtcNow.AddDays(-2));
            File.WriteAllText(recentTemp, "active candidate");
            var repository = new FilePendingMessageRepository(path);
            var now = DateTimeOffset.UtcNow;
            for (var index = 0; index < 40; index++) {
                var id = "delivery-" + index;
                await repository.SaveAsync(new PendingMessageRecord {
                    MessageId = id, Timestamp = now, NextAttemptAt = now
                });
                var lease = await repository.TryAcquireLeaseAsync(id, now, now.AddMinutes(1));
                Assert.NotNull(lease);
                Assert.True(await repository.TryRemoveWithLeaseAsync(id, lease!.ProcessingLeaseId!));
            }

            Assert.False(File.Exists(oldTemp));
            Assert.False(File.Exists(legacyTemp));
            Assert.True(File.Exists(recentTemp));
            Assert.True(File.ReadAllLines(path).Length < 40);
            var remaining = new List<PendingMessageRecord>();
            await foreach (var record in repository.GetAllAsync()) remaining.Add(record);
            Assert.Empty(remaining);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task NextAppendRepairsInterruptedLogTailBeforeWritingAnotherRecord() {
        var directory = Path.Combine(Path.GetTempPath(), "mailozaurr-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var path = Path.Combine(directory, "pending.log");
            var now = DateTimeOffset.UtcNow;
            await new FilePendingMessageRepository(path).SaveAsync(new PendingMessageRecord {
                MessageId = "first", Timestamp = now, NextAttemptAt = now
            });
            File.AppendAllText(path, "{\"EntryType\":\"upsert\",\"Record\":");

            var recovered = new FilePendingMessageRepository(path);
            await recovered.SaveAsync(new PendingMessageRecord {
                MessageId = "second", Timestamp = now, NextAttemptAt = now
            });

            var reopened = new FilePendingMessageRepository(path);
            Assert.NotNull(await reopened.GetByMessageIdAsync("first"));
            Assert.NotNull(await reopened.GetByMessageIdAsync("second"));
            Assert.DoesNotContain("{\"EntryType\":\"upsert\",\"Record\":", File.ReadAllText(path));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task OtherInstanceRebuildsItsIndexAfterCompactionReplacesTheLog() {
        var directory = Path.Combine(Path.GetTempPath(), "mailozaurr-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var path = Path.Combine(directory, "pending.log");
            var first = new FilePendingMessageRepository(path);
            var second = new FilePendingMessageRepository(path);
            var now = DateTimeOffset.UtcNow;
            var record = new PendingMessageRecord { MessageId = "kept", Timestamp = now, NextAttemptAt = now };
            await first.SaveAsync(record);
            Assert.NotNull(await second.GetByMessageIdAsync(record.MessageId));

            for (var attempt = 0; attempt < 65; attempt++) {
                record.AttemptCount = attempt;
                await first.SaveAsync(record);
            }

            var loaded = await second.GetByMessageIdAsync(record.MessageId);
            Assert.NotNull(loaded);
            Assert.Equal(64, loaded!.AttemptCount);
            Assert.NotNull(await second.TryAcquireLeaseAsync(record.MessageId, now, now.AddMinutes(1)));
            Assert.NotNull(await first.GetByMessageIdAsync(record.MessageId));
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
