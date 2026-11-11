using MailKit.Security;
using MimeKit;

namespace Mailozaurr.Tests;

public sealed class SmtpSubmissionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectSend_LostConfirmationDoesNotRetryOrQueue(bool retryAlways) {
        await using var server = new LoopbackSmtpServer(dropAfterData: true);
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr-submission", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Smtp? smtp = null;
        try {
            var queue = new FilePendingMessageRepository(Path.Combine(directory, "pending.jsonl"));
            smtp = await ConnectAsync(server, queue);
            smtp.RetryCount = 3;
            smtp.RetryAlways = retryAlways;
            var result = await smtp.SendAsync();

            Assert.False(result.Status);
            Assert.True(result.DeliveryUnknown);
            Assert.False(result.Queued);
            Assert.False(result.DeliveryAccepted);
            Assert.Empty(result.AcceptedRecipients);
            Assert.Equal(1, server.ContentSubmissions);
            Assert.Null(await queue.GetByMessageIdAsync(result.MessageId!));
        } finally {
            smtp?.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(250, true)]
    [InlineData(550, false)]
    public async Task DirectSend_ReportsConfirmedRecipientOutcome(int recipientCode, bool accepted) {
        await using var server = new LoopbackSmtpServer(recipientCode: recipientCode);
        var smtp = await ConnectAsync(server);
        try {
            var result = await smtp.SendAsync();

            Assert.Equal(accepted, result.Status);
            Assert.Equal(accepted, result.DeliveryAccepted);
            Assert.False(result.DeliveryUnknown);
            if (accepted) {
                Assert.Equal("recipient@example.invalid", Assert.Single(result.AcceptedRecipients));
                Assert.Empty(result.RejectedRecipients);
                Assert.Equal("content response", result.ResponseText);
            } else {
                Assert.Empty(result.AcceptedRecipients);
                Assert.Equal("recipient@example.invalid", Assert.Single(result.RejectedRecipients));
                Assert.Equal(0, server.ContentSubmissions);
            }
        } finally {
            smtp.Dispose();
        }
    }

    [Fact]
    public async Task QueuedSend_LostConfirmationRemainsHeldAfterRepositoryReload() {
        await using var server = new LoopbackSmtpServer(dropAfterData: true);
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr-submission", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var path = Path.Combine(directory, "pending.jsonl");
            var queue = new FilePendingMessageRepository(path);
            using var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse("sender@example.invalid"));
            message.To.Add(MailboxAddress.Parse("recipient@example.invalid"));
            message.Body = new TextPart("plain") { Text = "Synthetic queued submission" };
            using var stream = new MemoryStream();
            await message.WriteToAsync(stream);
            await queue.SaveAsync(new PendingMessageRecord {
                MessageId = "unknown@example.invalid", Server = "127.0.0.1", Port = server.Port,
                MimeMessage = Convert.ToBase64String(stream.ToArray()),
                ProviderData = new() { ["SecureSocketOptions"] = "None" }
            });
            await new PendingMessageProcessor(queue).ProcessAsync();
            var reloaded = new FilePendingMessageRepository(path);
            var held = await reloaded.GetByMessageIdAsync("unknown@example.invalid");
            Assert.NotNull(held);
            Assert.NotNull(held.DeliveryUnknownAt);
            Assert.Null(held.DeliveryAcceptedAt);
            Assert.Equal(1, held.AttemptCount);
            await new PendingMessageProcessor(reloaded).ProcessAsync();
            Assert.Equal(1, server.ContentSubmissions);
            Assert.Equal(1, server.Connections);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<Smtp> ConnectAsync(LoopbackSmtpServer server, IPendingMessageRepository? queue = null) {
        var smtp = new Smtp {
            From = "sender@example.invalid", To = new object[] { "recipient@example.invalid" },
            Subject = "Synthetic submission", TextBody = "Loopback only", Timeout = 2000,
            PendingMessageRepository = queue, RetryDelayMilliseconds = 0
        };
        await smtp.CreateMessageAsync();
        var connection = await smtp.ConnectAsync("127.0.0.1", server.Port, SecureSocketOptions.None);
        Assert.True(connection.Status);
        return smtp;
    }
}
