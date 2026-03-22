using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationGraphMailSendHandlerTests {
    [Fact]
    public async Task HandlerUsesInjectedSendDelegate() {
        var handler = new GraphMailSendHandler(
            new FakeGraphSessionFactory(),
            sendAsync: (session, profile, request, message, cancellationToken) => {
                Assert.Equal("sender@example.com", message.From.Mailboxes.First().Address);
                Assert.Equal("alice@example.com", message.To.Mailboxes.First().Address);
                Assert.Equal("Hello Graph", message.Subject);
                return Task.FromResult(new GraphMessage {
                    Id = "graph-123"
                });
            });

        var result = await handler.SendAsync(
            new MailProfile {
                Id = "work-graph",
                DisplayName = "Work Graph",
                Kind = MailProfileKind.Graph,
                DefaultSender = "sender@example.com",
                DefaultMailbox = "user@example.com"
            },
            new SendMessageRequest {
                ProfileId = "work-graph",
                RequireImmediateSend = true,
                Message = new DraftMessage {
                    Subject = "Hello Graph",
                    TextBody = "hello",
                    To = {
                        new MessageRecipient { Address = "alice@example.com" }
                    }
                }
            });

        Assert.True(result.Succeeded);
        Assert.False(result.Queued);
        Assert.Equal("work-graph", result.ProfileId);
        Assert.Equal(MailProfileKind.Graph, result.ProfileKind);
        Assert.Equal("graph-123", result.ProviderMessageId);
        Assert.NotNull(result.QueueMessageId);
    }

    [Fact]
    public async Task HandlerQueuesMessageWhenQueueIsPreferred() {
        var repository = new FilePendingMessageRepository(new PendingMessageRepositoryOptions {
            DirectoryPath = CreateTemporaryDirectory()
        });
        var handler = new GraphMailSendHandler(
            new FakeGraphSessionFactory(),
            pendingMessageRepository: repository,
            sendAsync: (session, profile, request, message, cancellationToken) => Task.FromResult(new GraphMessage {
                Id = "graph-123"
            }));

        var result = await handler.SendAsync(
            new MailProfile {
                Id = "work-graph",
                DisplayName = "Work Graph",
                Kind = MailProfileKind.Graph,
                DefaultSender = "sender@example.com",
                DefaultMailbox = "user@example.com"
            },
            new SendMessageRequest {
                ProfileId = "work-graph",
                Message = new DraftMessage {
                    Subject = "Hello Graph",
                    TextBody = "hello",
                    To = {
                        new MessageRecipient { Address = "alice@example.com" }
                    }
                }
            });

        Assert.True(result.Succeeded);
        Assert.True(result.Queued);
        Assert.NotNull(result.QueueMessageId);
        var queued = await repository.GetByMessageIdAsync(result.QueueMessageId!, CancellationToken.None);
        Assert.NotNull(queued);
        Assert.Equal(EmailProvider.Graph, queued!.Provider);
        Assert.Equal("user@example.com", queued.ProviderData[GraphPendingMessageSender.UserIdKey]);
    }

    [Fact]
    public async Task HandlerQueuesScheduledSendRequests() {
        var repository = new FilePendingMessageRepository(new PendingMessageRepositoryOptions {
            DirectoryPath = CreateTemporaryDirectory()
        });
        var handler = new GraphMailSendHandler(
            new FakeGraphSessionFactory(),
            pendingMessageRepository: repository,
            sendAsync: (session, profile, request, message, cancellationToken) => Task.FromResult(new GraphMessage {
                Id = "graph-123"
            }));

        var scheduledFor = DateTimeOffset.UtcNow.AddMinutes(15);
        var result = await handler.SendAsync(
            new MailProfile {
                Id = "work-graph",
                DisplayName = "Work Graph",
                Kind = MailProfileKind.Graph,
                DefaultSender = "sender@example.com",
                DefaultMailbox = "user@example.com",
                Settings = new Dictionary<string, string> {
                    [MailProfileSettingsKeys.ClientId] = "client-id",
                    [MailProfileSettingsKeys.TenantId] = "tenant-id"
                }
            },
            new SendMessageRequest {
                ProfileId = "work-graph",
                NotBefore = scheduledFor,
                Message = new DraftMessage {
                    Subject = "Hello Graph",
                    TextBody = "hello",
                    To = {
                        new MessageRecipient { Address = "alice@example.com" }
                    }
                }
            });

        Assert.True(result.Succeeded);
        Assert.True(result.Queued);
        var queued = await repository.GetByMessageIdAsync(result.QueueMessageId!, CancellationToken.None);
        Assert.NotNull(queued);
        Assert.Equal(EmailProvider.Graph, queued!.Provider);
        Assert.Equal("tenant-id", queued.ProviderData[GraphPendingMessageSender.TenantIdKey]);
        Assert.True(queued.NextAttemptAt >= scheduledFor.ToUniversalTime().AddSeconds(-1));
    }

    private sealed class FakeGraphSessionFactory : IGraphSessionFactory {
        public Task<GraphSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GraphSession(
                new GraphApiClient(new OAuthCredential {
                    UserName = profile.DefaultMailbox ?? "me",
                    AccessToken = "token",
                    ExpiresOn = DateTimeOffset.MaxValue
                }),
                profile.DefaultMailbox ?? "me",
                new OAuthCredential {
                    UserName = profile.DefaultMailbox ?? "me",
                    AccessToken = "token",
                    ExpiresOn = DateTimeOffset.MaxValue
                },
                new GraphCredential {
                    ClientId = "client-id",
                    DirectoryId = "tenant-id",
                    ClientSecret = "secret"
                }));
    }

    private static string CreateTemporaryDirectory() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
