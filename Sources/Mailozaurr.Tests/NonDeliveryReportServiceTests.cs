using Mailozaurr;
using Mailozaurr.NonDeliveryReports;
using System.Collections.Generic;
using System.Linq;
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
            if (messageId is null) {
                return Task.FromResult<SentMessageRecord?>(null);
            }
            store.TryGetValue(messageId, out SentMessageRecord? record);
            return Task.FromResult<SentMessageRecord?>(record);
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

    private sealed class ConcurrencyTestService : NonDeliveryReportServiceBase {
        private int current;
        private int maxConcurrent;

        public ConcurrencyTestService(SendLogResolver resolver) : base(resolver) { }

        public int MaxConcurrency => maxConcurrent;

        protected override async Task<IList<NonDeliveryReport>> SearchInternalAsync(
            DateTime? since,
            DateTime? before,
            string? recipientContains,
            string? messageId,
            int maxResults,
            CancellationToken cancellationToken) {
            int concurrency = Interlocked.Increment(ref current);
            int initial;
            do {
                initial = maxConcurrent;
                if (concurrency <= initial) {
                    break;
                }
            }
            while (Interlocked.CompareExchange(ref maxConcurrent, concurrency, initial) != initial);
            await Task.Delay(50, cancellationToken);
            Interlocked.Decrement(ref current);
            return Array.Empty<NonDeliveryReport>();
        }
    }

    [Fact]
    public async Task SearchAsync_ResolvesSentMessage() {
        var repo = new InMemorySentMessageRepository();
        var record = new SentMessageRecord { MessageId = "id1", Recipients = "user@example.com", Subject = "s", Timestamp = DateTimeOffset.UtcNow };
        await repo.SaveAsync(record);
        var resolver = new SendLogResolver(repo);
        var report = new NonDeliveryReport { OriginalMessageId = "id1", FinalRecipient = "user@example.com", Timestamp = DateTimeOffset.UtcNow };
        var service = new TestService(new List<NonDeliveryReport> { report }, resolver);

        IList<NonDeliveryReportResult> results = await service.SearchAsync();
        Assert.Single(results);
        Assert.Equal(report, results[0].Report);
        Assert.Equal(record, results[0].SentMessage);
    }

    [Fact]
    public async Task SearchAsync_SerializesInternalCalls() {
        var repo = new InMemorySentMessageRepository();
        var resolver = new SendLogResolver(repo);
        var service = new ConcurrencyTestService(resolver);

        Task[] tasks = Enumerable.Range(0, 5).Select(_ => service.SearchAsync()).ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(1, service.MaxConcurrency);
    }
}
