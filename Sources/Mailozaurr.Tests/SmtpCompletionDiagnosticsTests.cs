using MailKit.Security;
using MimeKit;

namespace Mailozaurr.Tests;

public sealed class SmtpCompletionDiagnosticsTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredQueue_DiagnosticFailureCannotScheduleAcceptedMailAgain(bool disconnectFailure) {
        await using var server = new LoopbackSmtpServer();
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr-completion", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var originalLogger = LoggingMessages.Logger;
        Smtp? smtp = null;
        int diagnosticFailures = 0;
        LoggingMessages.Logger = new InternalLogger();
        LoggingMessages.Logger.OnWarningMessage += (_, args) => {
            if (args.Message.Contains("Sent-log persistence") || args.Message.Contains("Disconnect failed") || args.Message.Contains("SMTP disconnect")) {
                diagnosticFailures++;
                throw new IOException("Diagnostic subscriber failed");
            }
        };
        try {
            var queue = new FilePendingMessageRepository(Path.Combine(directory, "pending.jsonl"));
            smtp = new Smtp { PendingMessageRepository = queue, RetryDelayMilliseconds = 0 };
            if (disconnectFailure) smtp.Client.Disconnected += (_, _) => throw new IOException("Disconnect observer failed");
            else smtp.SentMessageRepository = new FailingSentRepository();
            using var message = Message();
            using var stream = new MemoryStream();
            await message.WriteToAsync(stream);
            await queue.SaveAsync(new PendingMessageRecord {
                MessageId = "completion@example.invalid", Server = "127.0.0.1", Port = server.Port,
                MimeMessage = Convert.ToBase64String(stream.ToArray()),
                ProviderData = new() { ["SecureSocketOptions"] = "None" }
            });

            await smtp.ProcessPendingMessagesAsync();
            Assert.Null(await queue.GetByMessageIdAsync("completion@example.invalid"));
            await smtp.ProcessPendingMessagesAsync();
            Assert.Equal(1, server.ContentSubmissions);
            Assert.Equal(1, server.Accepted);
            Assert.True(diagnosticFailures > 0);
        } finally {
            smtp?.Dispose();
            LoggingMessages.Logger = originalLogger;
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeliverySession_SuccessDiagnosticFailurePreservesConfirmedAcceptance(bool ioFailure) {
        await using var server = new LoopbackSmtpServer();
        using var session = new SmtpDeliverySession();
        using var message = Message();
        var originalLogger = LoggingMessages.Logger;
        LoggingMessages.Logger = new InternalLogger();
        LoggingMessages.Logger.OnVerboseMessage += (_, args) => {
            if (args.Message.Contains("Sent email")) {
                if (ioFailure) throw new IOException("Success diagnostic failed");
                throw new InvalidOperationException("Success diagnostic failed");
            }
        };
        try {
            var result = await session.SendAsync(message,
                new SmtpSessionRequest { Authenticate = false, TimeoutMs = 2000 },
                new SmtpDeliveryOptions { Servers = new() {
                    new SmtpEndpoint { Server = "127.0.0.1", Port = server.Port, SecureSocketOptions = SecureSocketOptions.None }
                } }, smtp => smtp.Logging = new LoggingConfigurator());
            Assert.Equal(1, server.Accepted);
            Assert.True(result.Status, result.Error);
            Assert.True(result.DeliveryAccepted);
            Assert.False(result.DeliveryUnknown);
            Assert.Contains(result.PostSendErrors, error => error.Contains("Success diagnostic failed"));
            Assert.Single(result.Attempts);
            Assert.Equal(1, server.Accepted);
        } finally { LoggingMessages.Logger = originalLogger; }
    }

    private static MimeMessage Message() {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.invalid"));
        message.To.Add(MailboxAddress.Parse("recipient@example.invalid"));
        message.Body = new TextPart("plain") { Text = "Synthetic local completion test" };
        return message;
    }

    private sealed class FailingSentRepository : ISentMessageRepository {
        public Task SaveAsync(SentMessageRecord record, CancellationToken cancellationToken = default) =>
            Task.FromException(new IOException("Sent-log disk unavailable"));
        public Task<SentMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SentMessageRecord?>(null);
    }
}
