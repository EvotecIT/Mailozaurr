using MailKit.Security;
using MimeKit;

namespace Mailozaurr.Tests;

public sealed class SmtpSubmissionTests {
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task DirectSend_CompletionHandlerFailureDoesNotRetryOrQueue(int failureKind) {
        await using var server = new LoopbackSmtpServer();
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr-submission", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Smtp? smtp = null;
        try {
            var queue = new FilePendingMessageRepository(Path.Combine(directory, "pending.jsonl"));
            smtp = await ConnectAsync(server, queue);
            using var caller = new CancellationTokenSource();
            smtp.Client.MessageSent += (_, _) => {
                if (failureKind == 2) {
                    caller.Cancel();
                    throw new OperationCanceledException("Completion observer failed", caller.Token);
                }
                throw failureKind == 1 ? new IOException("Completion observer failed") : new InvalidOperationException("Completion observer failed");
            };
            smtp.RetryAlways = true;
            smtp.RetryCount = 2;
            var result = await smtp.SendAsync(caller.Token);
            Assert.True(result.Status);
            Assert.True(result.DeliveryAccepted);
            Assert.False(result.DeliveryUnknown);
            Assert.False(result.Queued);
            Assert.Equal(1, server.Accepted);
            Assert.Equal(1, server.ContentSubmissions);
            Assert.Contains("Completion observer failed", Assert.Single(result.PostSendErrors));
            Assert.Null(await queue.GetByMessageIdAsync(result.MessageId!));
        } finally { smtp?.Dispose(); Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedSend_CompletionHandlerFailureDoesNotBecomeARejectedDelivery(bool warningHandlerThrows) {
        await using var server = new LoopbackSmtpServer();
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr-submission", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Smtp? smtp = null;
        try {
            var queue = new FilePendingMessageRepository(Path.Combine(directory, "pending.jsonl"));
            smtp = await ConnectAsync(server);
            using var stream = new MemoryStream();
            await smtp.Message.WriteToAsync(stream);
            await queue.SaveAsync(new PendingMessageRecord {
                MessageId = "completion@example.invalid", Server = "127.0.0.1", Port = server.Port,
                MimeMessage = Convert.ToBase64String(stream.ToArray()), ProviderData = new() { ["SecureSocketOptions"] = "None" }
            });
            var sender = new SmtpPendingMessageSender(() => {
                var client = new ClientSmtp();
                client.MessageSent += (_, _) => throw new InvalidOperationException("Completion observer failed");
                return client;
            });
            var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> { [EmailProvider.None] = sender });
            var observer = new AcceptanceObserver();
            var logger = new InternalLogger();
            logger.OnWarningMessage += (_, _) => {
                if (warningHandlerThrows) throw new InvalidOperationException("Warning observer failed");
            };
            await new PendingMessageProcessor(queue, factory, logger: logger, observer: observer).ProcessAsync();
            Assert.NotNull(observer.Sent);
            Assert.NotNull(observer.Sent!.DeliveryAcceptedAt);
            Assert.Null(observer.Sent.DeliveryUnknownAt);
            Assert.Contains("Completion observer failed", Assert.Single(observer.Sent!.PostSendErrors));
            Assert.Equal(1, server.Accepted);
            Assert.Null(await queue.GetByMessageIdAsync("completion@example.invalid"));
            await new PendingMessageProcessor(queue, factory, observer: observer).ProcessAsync();
            Assert.Equal(1, server.ContentSubmissions);
        } finally { smtp?.Dispose(); Directory.Delete(directory, recursive: true); }
    }

    private sealed class AcceptanceObserver : IPendingMessageProcessorObserver {
        internal PendingMessageRecord? Sent;
        public void MessageSent(PendingMessageRecord record, int attempt, TimeSpan duration) => Sent = record.Clone();
        public void MessageSkipped(PendingMessageRecord record, PendingMessageSkipReason reason) { }
        public void MessageAttemptStarted(PendingMessageRecord record, int attempt) { }
        public void MessageFailed(PendingMessageRecord record, int attempt, Exception exception, TimeSpan duration, bool willRetry, TimeSpan? retryDelay) { }
        public void MessageDropped(PendingMessageRecord record, int attempt, PendingMessageDropReason reason, Exception? exception) { }
    }

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedSend_LostConfirmationRemainsHeldAfterRepositoryReload(bool customSender) {
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
            var sender = new UnknownSender();
            var factory = customSender ? new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> { [EmailProvider.None] = sender }) : null;
            await new PendingMessageProcessor(queue, factory).ProcessAsync();
            var reloaded = new FilePendingMessageRepository(path);
            var held = await reloaded.GetByMessageIdAsync("unknown@example.invalid");
            Assert.NotNull(held);
            Assert.NotNull(held.DeliveryUnknownAt);
            Assert.Null(held.DeliveryAcceptedAt);
            Assert.Equal(1, held.AttemptCount);
            await new PendingMessageProcessor(reloaded, factory).ProcessAsync();
            Assert.Equal(customSender ? 0 : 1, server.ContentSubmissions);
            Assert.Equal(customSender ? 0 : 1, server.Connections);
            if (customSender) Assert.Equal(1, sender.Calls);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class UnknownSender : IPendingMessageSender {
        internal int Calls;
        public Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
            Calls++;
            throw new SmtpDeliveryUnknownException(new IOException("Custom transport lost confirmation"));
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
