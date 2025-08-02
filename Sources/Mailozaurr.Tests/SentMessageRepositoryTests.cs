using Mailozaurr.NonDeliveryReports;

namespace Mailozaurr.Tests;

public class SentMessageRepositoryTests {
    [Fact]
    public async Task ResolverMatchesSavedRecord() {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
        try {
            var repo = new FileSentMessageRepository(path);
            var record = new SentMessageRecord {
                MessageId = "id1",
                Recipients = "c@d.com",
                Subject = "subject",
                Timestamp = DateTimeOffset.UtcNow
            };
            await repo.SaveAsync(record);
            var resolver = new SendLogResolver(repo);
            var ndr = new NonDeliveryReport { OriginalMessageId = "id1", FinalRecipient = "c@d.com" };
            var resolved = await resolver.ResolveAsync(ndr);
            Assert.NotNull(resolved);
            Assert.Equal("subject", resolved!.Subject);
        } finally {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsync_AppendsMultipleRecords() {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
        try {
            var repo = new FileSentMessageRepository(path);
            await repo.SaveAsync(new SentMessageRecord { MessageId = "1", Recipients = "a@b.com", Subject = "s1", Timestamp = DateTimeOffset.UtcNow });
            await repo.SaveAsync(new SentMessageRecord { MessageId = "2", Recipients = "c@d.com", Subject = "s2", Timestamp = DateTimeOffset.UtcNow });
            var record = await repo.GetByMessageIdAsync("2");
            Assert.NotNull(record);
            Assert.Equal("s2", record!.Subject);
        } finally {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
