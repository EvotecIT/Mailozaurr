using System.Collections.Generic;

namespace Mailozaurr.Tests;

public sealed class FilePendingMessageDeadLetterRepositoryTests {
    [Fact]
    public async Task RemovingMissingRecordFromUncreatedDirectoryIsAnImmediateNoOp() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        var repository = new FilePendingMessageDeadLetterRepository(
            Path.Combine(directory, "dead-letter.log"));

        await repository.RemoveAsync("missing");

        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task SaveGetListAndRemoveRoundTripsDeadLetterRecord() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(directory, "dead-letter.log");

        try {
            var repository = new FilePendingMessageDeadLetterRepository(filePath);
            var record = new PendingMessageDeadLetterRecord {
                Message = new PendingMessageRecord {
                    MessageId = "queued-1",
                    Provider = EmailProvider.Gmail,
                    AttemptCount = 2,
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10)
                },
                Reason = PendingMessageDropReason.PermanentFailure,
                Attempt = 2,
                DeadLetteredAt = DateTimeOffset.UtcNow,
                ExceptionType = typeof(InvalidOperationException).FullName,
                ErrorMessage = "Authentication failed."
            };

            await repository.SaveAsync(record);

            var loaded = await repository.GetByMessageIdAsync("queued-1");
            var all = await ReadAllAsync(repository);

            Assert.NotNull(loaded);
            Assert.Equal(PendingMessageDropReason.PermanentFailure, loaded!.Reason);
            Assert.Equal("Authentication failed.", loaded.ErrorMessage);
            Assert.Single(all);

            await repository.RemoveAsync("queued-1");

            Assert.Null(await repository.GetByMessageIdAsync("queued-1"));
        } finally {
            if (Directory.Exists(directory)) {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task OptionsDeriveDeadLetterFileNameFromPendingFileNamingScheme() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        var options = new PendingMessageRepositoryOptions {
            DirectoryPath = directory,
            FileNamingScheme = () => "tenant-a.log"
        };

        try {
            var repository = new FilePendingMessageDeadLetterRepository(options);

            await repository.SaveAsync(new PendingMessageDeadLetterRecord {
                Message = new PendingMessageRecord {
                    MessageId = "tenant-a-message",
                    Provider = EmailProvider.Gmail,
                    Timestamp = DateTimeOffset.UtcNow
                },
                Reason = PendingMessageDropReason.PermanentFailure,
                Attempt = 1,
                DeadLetteredAt = DateTimeOffset.UtcNow,
                ErrorMessage = "Permanent failure."
            });

            Assert.True(File.Exists(Path.Combine(directory, "tenant-a.dead-letter.log")));
            Assert.False(File.Exists(Path.Combine(directory, "dead-letter.log")));
        } finally {
            if (Directory.Exists(directory)) {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveRepairsInterruptedTailBeforeAnotherTerminalRecord() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "dead-letter.log");
        try {
            var repository = new FilePendingMessageDeadLetterRepository(path);
            await repository.SaveAsync(CreateRecord("first"));
            File.AppendAllText(path, "{\"Message\":");

            await new FilePendingMessageDeadLetterRepository(path).SaveAsync(CreateRecord("second"));

            var records = await ReadAllAsync(new FilePendingMessageDeadLetterRepository(path));
            Assert.Equal(new[] { "first", "second" }, records.Select(record => record.Message.MessageId));
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RepeatedSaveOfSameMessageIdIsIdempotent() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "dead-letter.log");
        try {
            var first = new FilePendingMessageDeadLetterRepository(path);
            var second = new FilePendingMessageDeadLetterRepository(path);

            await first.SaveAsync(CreateRecord("replayed-terminal"));
            await second.SaveAsync(CreateRecord("replayed-terminal"));

            var records = await ReadAllAsync(new FilePendingMessageDeadLetterRepository(path));
            Assert.Equal("replayed-terminal", Assert.Single(records).Message.MessageId);
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task IdempotencyIndexTracksOtherInstancesAndRewrites() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "dead-letter.log");
        try {
            var first = new FilePendingMessageDeadLetterRepository(path);
            var second = new FilePendingMessageDeadLetterRepository(path);
            await first.SaveAsync(CreateRecord("first"));
            await second.SaveAsync(CreateRecord("second"));
            await first.SaveAsync(CreateRecord("second"));
            Assert.Equal(2, (await ReadAllAsync(first)).Count);

            await second.RemoveAsync("first");
            await first.SaveAsync(CreateRecord("first"));
            await second.SaveAsync(CreateRecord("first"));

            var records = await ReadAllAsync(new FilePendingMessageDeadLetterRepository(path));
            Assert.Equal(2, records.Count);
            Assert.Equal(1, records.Count(record => record.Message.MessageId == "first"));
            Assert.Equal(1, records.Count(record => record.Message.MessageId == "second"));
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SeparateInstancesSerializeRemovalAndNewTerminalSave() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "dead-letter.log");
        try {
            var first = new FilePendingMessageDeadLetterRepository(path);
            var second = new FilePendingMessageDeadLetterRepository(path);
            await first.SaveAsync(CreateRecord("remove-me"));
            using (var held = new FileStream(path + ".lock", FileMode.Open,
                FileAccess.ReadWrite, FileShare.None)) {
                var remove = first.RemoveAsync("remove-me");
                var save = second.SaveAsync(CreateRecord("keep-me"));
                await Task.Delay(100);
                Assert.False(remove.IsCompleted);
                Assert.False(save.IsCompleted);
                held.Dispose();
                await Task.WhenAll(remove, save);
            }
            var records = await ReadAllAsync(new FilePendingMessageDeadLetterRepository(path));
            Assert.Equal("keep-me", Assert.Single(records).Message.MessageId);
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static PendingMessageDeadLetterRecord CreateRecord(string id) => new() {
        Message = new PendingMessageRecord {
            MessageId = id, Provider = EmailProvider.Gmail, Timestamp = DateTimeOffset.UtcNow
        },
        Reason = PendingMessageDropReason.PermanentFailure,
        Attempt = 1,
        DeadLetteredAt = DateTimeOffset.UtcNow
    };

    private static async Task<List<PendingMessageDeadLetterRecord>> ReadAllAsync(IPendingMessageDeadLetterRepository repository) {
        var records = new List<PendingMessageDeadLetterRecord>();
        await foreach (var record in repository.GetAllAsync()) {
            records.Add(record);
        }

        return records;
    }
}
