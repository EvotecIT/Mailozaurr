using Mailozaurr.Application;
using System.Net.Http;

namespace Mailozaurr.Tests;

public sealed class ApplicationGmailMailSendHandlerTests {
    [Fact]
    public async Task HandlerUsesInjectedSendDelegate() {
        var handler = new GmailMailSendHandler(
            new FakeGmailSessionFactory(),
            sendAsync: (session, profile, request, message, cancellationToken) => {
                Assert.Equal("sender@example.com", message.From.Mailboxes.First().Address);
                Assert.Equal("alice@example.com", message.To.Mailboxes.First().Address);
                Assert.Equal("Hello Gmail", message.Subject);
                return Task.FromResult(new GmailMessage {
                    Id = "gmail-123",
                    ThreadId = "thread-123"
                });
            });

        var result = await handler.SendAsync(
            new MailProfile {
                Id = "personal-gmail",
                DisplayName = "Personal Gmail",
                Kind = MailProfileKind.Gmail,
                DefaultSender = "sender@example.com",
                DefaultMailbox = "me"
            },
            new SendMessageRequest {
                ProfileId = "personal-gmail",
                Message = new DraftMessage {
                    Subject = "Hello Gmail",
                    TextBody = "hello",
                    To = {
                        new MessageRecipient { Address = "alice@example.com" }
                    }
                }
            });

        Assert.True(result.Succeeded);
        Assert.False(result.Queued);
        Assert.Equal("personal-gmail", result.ProfileId);
        Assert.Equal(MailProfileKind.Gmail, result.ProfileKind);
        Assert.Equal("gmail-123", result.ProviderMessageId);
    }

    [Fact]
    public async Task HandlerReturnsQueuedResultWhenRepositoryCapturesFailedSend() {
        var repository = new FilePendingMessageRepository(new PendingMessageRepositoryOptions {
            DirectoryPath = CreateTemporaryDirectory()
        });

        var handler = new GmailMailSendHandler(
            new FakeGmailSessionFactory(),
            pendingMessageRepository: repository,
            sendAsync: async (session, profile, request, message, cancellationToken) => {
                await repository.SaveAsync(new PendingMessageRecord {
                    MessageId = message.MessageId ?? "queued-1",
                    Timestamp = DateTimeOffset.UtcNow,
                    NextAttemptAt = DateTimeOffset.UtcNow,
                    Provider = EmailProvider.Gmail,
                    MimeMessage = Convert.ToBase64String(Array.Empty<byte>())
                }, cancellationToken).ConfigureAwait(false);

                throw new HttpRequestException("temporary gmail failure");
            });

        var result = await handler.SendAsync(
            new MailProfile {
                Id = "personal-gmail",
                DisplayName = "Personal Gmail",
                Kind = MailProfileKind.Gmail,
                DefaultSender = "sender@example.com",
                DefaultMailbox = "me"
            },
            new SendMessageRequest {
                ProfileId = "personal-gmail",
                QueueOnFailure = true,
                Message = new DraftMessage {
                    Subject = "Hello Gmail",
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

    private sealed class FakeGmailSessionFactory : IGmailSessionFactory {
        public Task<GmailSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GmailSession(new GmailApiClient(new OAuthCredential {
                UserName = profile.DefaultMailbox ?? "me",
                AccessToken = "token",
                ExpiresOn = DateTimeOffset.MaxValue
            }), profile.DefaultMailbox ?? "me"));
    }
}
