using System.Reflection;
using MailKit;
using MimeKit;

namespace Mailozaurr.Tests;

public sealed class SmtpPendingMessageTests {
    private sealed class FailClient : ClientSmtp {
        public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null) {
            throw new Exception("fail");
        }
    }

    private sealed class SuccessClient : ClientSmtp {
        public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null) {
            return Task.FromResult(string.Empty);
        }
    }

    private sealed class InMemoryPendingRepository : IPendingMessageRepository {
        public List<PendingMessageRecord> Saved { get; } = new();
        public List<string> Removed { get; } = new();

        public Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) {
            Saved.Add(record);
            return Task.CompletedTask;
        }

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.FirstOrDefault(r => r.MessageId == messageId));

        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync(CancellationToken cancellationToken = default) {
            foreach (var r in Saved) {
                yield return r;
                await Task.Yield();
            }
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
            Removed.Add(messageId);
            return Task.CompletedTask;
        }
    }

    private static void SetClient(Smtp smtp, ClientSmtp client) {
        var field = typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(smtp, client);
    }

    [Fact]
    public async Task SendFailureQueuesMessage() {
        var repo = new InMemoryPendingRepository();
        var smtp = new Smtp { PendingMessageRepository = repo, RetryCount = 0 };
        SetClient(smtp, new FailClient());
        smtp.From = "a@b.com";
        smtp.To = new object[] { "b@c.com" };
        smtp.Subject = "test";
        smtp.TextBody = "body";
        smtp.CreateMessage();

        var result = await smtp.SendAsync();

        Assert.False(result.Status);
        Assert.NotNull(result.MessageId);
        Assert.Single(repo.Saved);
        Assert.Equal(result.MessageId, repo.Saved[0].MessageId);
        Assert.False(string.IsNullOrWhiteSpace(repo.Saved[0].MimeMessage));
    }

    [Fact]
    public async Task SuccessfulSendRemovesQueuedMessage() {
        var repo = new InMemoryPendingRepository();
        var smtp = new Smtp { PendingMessageRepository = repo };
        SetClient(smtp, new SuccessClient());
        smtp.From = "a@b.com";
        smtp.To = new object[] { "b@c.com" };
        smtp.Subject = "test";
        smtp.TextBody = "body";
        smtp.CreateMessage();
        smtp.Message.MessageId = "msg-1";

        var result = await smtp.SendAsync();

        Assert.True(result.Status);
        Assert.Single(repo.Removed);
        Assert.Equal("msg-1", repo.Removed[0]);
    }
}

