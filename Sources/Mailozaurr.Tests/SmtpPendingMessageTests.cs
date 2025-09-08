using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
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
            if (record.NextAttemptAt == default) {
                record.NextAttemptAt = DateTimeOffset.UtcNow;
            }
            var existing = Saved.FindIndex(r => r.MessageId == record.MessageId);
            if (existing >= 0) {
                Saved[existing] = record;
            } else {
                Saved.Add(record);
            }
            return Task.CompletedTask;
        }

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.FirstOrDefault(r => r.MessageId == messageId));

        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync(CancellationToken cancellationToken = default) {
            var snapshot = Saved.ToList();
            foreach (var r in snapshot) {
                yield return r;
                await Task.Yield();
            }
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
            Removed.Add(messageId);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySentRepository : ISentMessageRepository {
        public List<SentMessageRecord> Saved { get; } = new();

        public Task SaveAsync(SentMessageRecord record, CancellationToken cancellationToken = default) {
            Saved.Add(record);
            return Task.CompletedTask;
        }

        public Task<SentMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.FirstOrDefault(r => r.MessageId == messageId));
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
    public async Task QueuedMessagePasswordIsEncrypted() {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return;
        }
        var repo = new InMemoryPendingRepository();
        var smtp = new Smtp { PendingMessageRepository = repo, RetryCount = 0 };
        SetClient(smtp, new FailClient());
        smtp.From = "a@b.com";
        smtp.To = new object[] { "b@c.com" };
        smtp.Subject = "test";
        smtp.TextBody = "body";
        smtp.CreateMessage();
        smtp.Authenticate("user", "secret", false);

        await smtp.SendAsync();

        var record = Assert.Single(repo.Saved);
        Assert.NotEqual("secret", record.Password);
        var bytes = Convert.FromBase64String(record.Password!);
        var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
        Assert.Equal("secret", Encoding.UTF8.GetString(decrypted));
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

    [Fact]
    public void SettingPendingMessagesPathCreatesRepository() {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var smtp = new Smtp { PendingMessagesPath = path };
        Assert.IsType<FilePendingMessageRepository>(smtp.PendingMessageRepository);
        Assert.Equal(path, ((FilePendingMessageRepository)smtp.PendingMessageRepository!).FilePath);
    }

    [Fact]
    public async Task ProcessPendingMessages_SendsAndLogs() {
        var pending = new InMemoryPendingRepository();
        var sent = new InMemorySentRepository();
        var smtp = new Smtp { PendingMessageRepository = pending, SentMessageRepository = sent };
        SetClient(smtp, new SuccessClient());

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("a@b.com"));
        message.To.Add(MailboxAddress.Parse("b@c.com"));
        message.Subject = "queued";
        message.Body = new TextPart("plain") { Text = "body" };
        message.MessageId = "msg-1";
        using (var ms = new MemoryStream()) {
            await message.WriteToAsync(ms);
            var record = new PendingMessageRecord {
                MessageId = "msg-1",
                MimeMessage = Convert.ToBase64String(ms.ToArray()),
                Timestamp = DateTimeOffset.UtcNow
            };
            await pending.SaveAsync(record);
        }

        await smtp.ProcessPendingMessagesAsync();

        Assert.Single(pending.Removed);
        Assert.Equal("msg-1", pending.Removed[0]);
        Assert.Single(sent.Saved);
        Assert.Equal("msg-1", sent.Saved[0].MessageId);
    }

    [Fact]
    public async Task ProcessPendingMessages_FailedSendRetainsMessage() {
        var pending = new InMemoryPendingRepository();
        var sent = new InMemorySentRepository();
        var smtp = new Smtp { PendingMessageRepository = pending, SentMessageRepository = sent };
        SetClient(smtp, new FailClient());

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("a@b.com"));
        message.To.Add(MailboxAddress.Parse("b@c.com"));
        message.Subject = "queued";
        message.Body = new TextPart("plain") { Text = "body" };
        message.MessageId = "msg-2";
        using (var ms = new MemoryStream()) {
            await message.WriteToAsync(ms);
            var record = new PendingMessageRecord {
                MessageId = "msg-2",
                MimeMessage = Convert.ToBase64String(ms.ToArray()),
                Timestamp = DateTimeOffset.UtcNow
            };
            await pending.SaveAsync(record);
        }

        await smtp.ProcessPendingMessagesAsync();

        Assert.Empty(pending.Removed);
        Assert.Empty(sent.Saved);
    }

    [Fact]
    public async Task ProcessPendingMessages_SkipsFutureNextAttempt() {
        var pending = new InMemoryPendingRepository();
        var sent = new InMemorySentRepository();
        var smtp = new Smtp { PendingMessageRepository = pending, SentMessageRepository = sent };
        SetClient(smtp, new SuccessClient());

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("a@b.com"));
        message.To.Add(MailboxAddress.Parse("b@c.com"));
        message.Subject = "queued";
        message.Body = new TextPart("plain") { Text = "body" };
        message.MessageId = "msg-3";
        using (var ms = new MemoryStream()) {
            await message.WriteToAsync(ms);
            var record = new PendingMessageRecord {
                MessageId = "msg-3",
                MimeMessage = Convert.ToBase64String(ms.ToArray()),
                Timestamp = DateTimeOffset.UtcNow,
                NextAttemptAt = DateTimeOffset.UtcNow.AddDays(1)
            };
            await pending.SaveAsync(record);
        }

        await smtp.ProcessPendingMessagesAsync();

        Assert.Empty(sent.Saved);
        Assert.Empty(pending.Removed);
    }

    [Fact]
    public async Task ProcessPendingMessages_SendsWhenNextAttemptReached() {
        var pending = new InMemoryPendingRepository();
        var sent = new InMemorySentRepository();
        var smtp = new Smtp { PendingMessageRepository = pending, SentMessageRepository = sent };
        SetClient(smtp, new SuccessClient());

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("a@b.com"));
        message.To.Add(MailboxAddress.Parse("b@c.com"));
        message.Subject = "queued";
        message.Body = new TextPart("plain") { Text = "body" };
        message.MessageId = "msg-4";
        using (var ms = new MemoryStream()) {
            await message.WriteToAsync(ms);
            var record = new PendingMessageRecord {
                MessageId = "msg-4",
                MimeMessage = Convert.ToBase64String(ms.ToArray()),
                Timestamp = DateTimeOffset.UtcNow,
                NextAttemptAt = DateTimeOffset.UtcNow.AddDays(1)
            };
            await pending.SaveAsync(record);
        }

        await smtp.ProcessPendingMessagesAsync();
        Assert.Empty(sent.Saved);

        var existing = await pending.GetByMessageIdAsync("msg-4");
        Assert.NotNull(existing);
        existing!.NextAttemptAt = DateTimeOffset.UtcNow;
        await pending.SaveAsync(existing);

        await smtp.ProcessPendingMessagesAsync();

        Assert.Single(sent.Saved);
        Assert.Equal("msg-4", sent.Saved[0].MessageId);
        Assert.Single(pending.Removed);
        Assert.Equal("msg-4", pending.Removed[0]);
    }
}

