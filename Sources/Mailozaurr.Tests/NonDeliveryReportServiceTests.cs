using Mailozaurr;
using Mailozaurr.NonDeliveryReports;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class NonDeliveryReportServiceTests {
    private sealed class InMemorySentMessageRepository : ISentMessageRepository {
        private readonly Dictionary<string, SentMessageRecord> store = new();

        public Task SaveAsync(SentMessageRecord record, CancellationToken cancellationToken = default) {
            store[record.MessageId] = record;
            return Task.CompletedTask;
        }

        public Task<SentMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) {
            store.TryGetValue(messageId, out SentMessageRecord? record);
            return Task.FromResult(record);
        }
    }

    private sealed class TestService : NonDeliveryReportServiceBase {
        private readonly IList<NonDeliveryReport> reports;

        public TestService(IList<NonDeliveryReport> reports, SendLogResolver resolver) : base(resolver) => this.reports = reports;

        protected override Task<IList<NonDeliveryReport>> SearchInternalAsync(
            DateTime? since,
            DateTime? before,
            string? recipientContains,
            string? messageId,
            int maxResults,
            CancellationToken cancellationToken) => Task.FromResult(reports);
    }

    [Fact]
    public async Task SearchAsync_ResolvesSentMessage() {
        var repo = new InMemorySentMessageRepository();
        var record = new SentMessageRecord { MessageId = "<id1>", Recipients = "user@example.com", Subject = "s", Timestamp = DateTimeOffset.UtcNow };
        await repo.SaveAsync(record);
        var resolver = new SendLogResolver(repo);
        var report = new NonDeliveryReport { OriginalMessageId = "<id1>", FinalRecipient = "user@example.com", Timestamp = DateTimeOffset.UtcNow };
        var service = new TestService(new List<NonDeliveryReport> { report }, resolver);

        IList<NonDeliveryReportResult> results = await service.SearchAsync();
        Assert.Single(results);
        Assert.Equal(report, results[0].Report);
        Assert.Equal(record, results[0].SentMessage);
    }
}
