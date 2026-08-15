using Mailozaurr;
using Mailozaurr.Hosting;

namespace Mailozaurr.Tests;

public sealed class ApplicationSmtpMailSendHandlerTests {
    [Fact]
    public async Task HandlerUsesInjectedSendDelegate() {
        var handler = new SmtpMailSendHandler(
            new FakeSmtpSessionFactory(),
            sendAsync: (session, profile, request, message, cancellationToken) => {
                Assert.Equal("sender@example.com", message.From.Mailboxes.First().Address);
                Assert.Equal("alice@example.com", message.To.Mailboxes.First().Address);
                Assert.Equal("Hello SMTP", message.Subject);
                return Task.FromResult(new SmtpResult(true, EmailAction.Send, "alice@example.com", "sender@example.com", "smtp.example.com", 587, TimeSpan.Zero) {
                    MessageId = "smtp-msg-123"
                });
            });

        var result = await handler.SendAsync(
            new MailProfile {
                Id = "work-smtp",
                DisplayName = "Work SMTP",
                Kind = MailProfileKind.Smtp,
                DefaultSender = "sender@example.com",
                Settings = new Dictionary<string, string> {
                    [MailProfileSettingsKeys.Server] = "smtp.example.com"
                }
            },
            new SendMessageRequest {
                ProfileId = "work-smtp",
                Message = new DraftMessage {
                    Subject = "Hello SMTP",
                    TextBody = "hello",
                    To = {
                        new MessageRecipient { Address = "alice@example.com" }
                    }
                }
            });

        Assert.True(result.Succeeded);
        Assert.False(result.Queued);
        Assert.Equal("work-smtp", result.ProfileId);
        Assert.Equal(MailProfileKind.Smtp, result.ProfileKind);
        Assert.Equal("smtp-msg-123", result.ProviderMessageId);
    }

    [Fact]
    public async Task HandlerPreservesConfirmedQueueAfterRecordIsConsumed() {
        var repository = new FilePendingMessageRepository(new PendingMessageRepositoryOptions {
            DirectoryPath = CreateTemporaryDirectory()
        });

        var handler = new SmtpMailSendHandler(
            new FakeSmtpSessionFactory(),
            pendingMessageRepository: repository,
            sendAsync: async (session, profile, request, message, cancellationToken) => {
                await repository.SaveAsync(new PendingMessageRecord {
                    MessageId = message.MessageId ?? "queued-1",
                    Timestamp = DateTimeOffset.UtcNow,
                    NextAttemptAt = DateTimeOffset.UtcNow,
                    Provider = EmailProvider.None,
                    MimeMessage = Convert.ToBase64String(Array.Empty<byte>())
                }, cancellationToken).ConfigureAwait(false);
                await repository.RemoveAsync(message.MessageId ?? "queued-1", cancellationToken).ConfigureAwait(false);

                return new SmtpResult(false, EmailAction.Send, "alice@example.com", "sender@example.com", "smtp.example.com", 587, TimeSpan.Zero, error: "temporary smtp failure") {
                    MessageId = message.MessageId ?? "queued-1",
                    Queued = true
                };
            });

        var result = await handler.SendAsync(
            new MailProfile {
                Id = "work-smtp",
                DisplayName = "Work SMTP",
                Kind = MailProfileKind.Smtp,
                DefaultSender = "sender@example.com",
                Settings = new Dictionary<string, string> {
                    [MailProfileSettingsKeys.Server] = "smtp.example.com"
                }
            },
            new SendMessageRequest {
                ProfileId = "work-smtp",
                QueueOnFailure = true,
                Message = new DraftMessage {
                    Subject = "Hello SMTP",
                    TextBody = "hello",
                    To = {
                        new MessageRecipient { Address = "alice@example.com" }
                    }
                }
            });

        Assert.True(result.Succeeded);
        Assert.True(result.Queued);
        Assert.NotNull(result.QueueMessageId);
        Assert.Contains("queued", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTemporaryDirectory() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FakeSmtpSessionFactory : ISmtpSessionFactory {
        public Task<Smtp> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Smtp());
    }
}
