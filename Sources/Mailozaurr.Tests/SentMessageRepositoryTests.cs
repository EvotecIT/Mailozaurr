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
}
