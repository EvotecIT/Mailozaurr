using System.Collections.Generic;

namespace Mailozaurr.Tests;

public sealed class FilePendingMessageDeadLetterRepositoryTests {
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

    private static async Task<List<PendingMessageDeadLetterRecord>> ReadAllAsync(IPendingMessageDeadLetterRepository repository) {
        var records = new List<PendingMessageDeadLetterRecord>();
        await foreach (var record in repository.GetAllAsync()) {
            records.Add(record);
        }

        return records;
    }
}
