using Mailozaurr.NonDeliveryReports;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class SendLogResolverTests {
    private sealed class InMemoryRepository : ISentMessageRepository {
        private readonly SentMessageRecord record;
        public InMemoryRepository(SentMessageRecord record) => this.record = record;
        public Task SaveAsync(SentMessageRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<SentMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default)
            => Task.FromResult(record.MessageId == messageId ? record : null);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsMatchingRecord() {
        var record = new SentMessageRecord { MessageId = "id1", Recipients = "user@example.com", Subject = "s", Timestamp = DateTimeOffset.UtcNow };
        var repo = new InMemoryRepository(record);
        var resolver = new SendLogResolver(repo);
        var report = new NonDeliveryReport { OriginalMessageId = "id1" };
        var result = await resolver.ResolveAsync(report);
        Assert.Equal(record, result);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNullWhenNotFound() {
        var record = new SentMessageRecord { MessageId = "id1", Recipients = "user@example.com" };
        var repo = new InMemoryRepository(record);
        var resolver = new SendLogResolver(repo);
        var report = new NonDeliveryReport { OriginalMessageId = "other" };
        var result = await resolver.ResolveAsync(report);
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_MatchesRecipientWithPrefix() {
        var record = new SentMessageRecord { MessageId = "id1", Recipients = "user@example.com" };
        var repo = new InMemoryRepository(record);
        var resolver = new SendLogResolver(repo);
        var headers = new Dictionary<string, string> {
            ["Original-Message-ID"] = "<id1>",
            ["Final-Recipient"] = "rfc822; user@example.com"
        };
        var report = NonDeliveryReport.FromHeaders(headers);
        var result = await resolver.ResolveAsync(report);
        Assert.Equal(record, result);
    }

    [Fact]
    public async Task ResolveAsync_MatchesLegacyFormattedRecipientsWithDisplayNameComma() {
        var record = new SentMessageRecord {
            MessageId = "id1",
            Recipients = "\"Doe, Jane\" <jane@example.com>, John Smith <john@example.com>"
        };
        var repo = new InMemoryRepository(record);
        var resolver = new SendLogResolver(repo);
        var report = new NonDeliveryReport {
            OriginalMessageId = "id1",
            FinalRecipientAddress = "jane@example.com"
        };

        var result = await resolver.ResolveAsync(report);

        Assert.Equal(record, result);
    }
}