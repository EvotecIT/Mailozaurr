using MailKit.Net.Imap;
using Mailozaurr;
using System.Net;
using System.Net.Http;

namespace Mailozaurr.Tests;

public sealed class MailChangeFeedServiceTests {
    [Fact]
    public async Task GraphDeltaMapsUpsertsDeletesAndDurableCursor() {
        const string body = "{\"@odata.deltaLink\":\"https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages/delta?$deltatoken=next\",\"value\":[{\"id\":\"up-1\",\"subject\":\"hello\",\"conversationId\":\"thread-1\"},{\"id\":\"gone-1\",\"@removed\":{}}]}";
        var handler = new RecordingHandler(Response(HttpStatusCode.OK, body));
        var service = await CreateAsync(MailProfileKind.Graph, new HttpGraphSessionFactory(handler));

        var result = await service.GetChangesAsync(new MailChangeFeedRequest {
            ProfileId = "profile",
            FolderId = "INBOX",
            MaxChanges = 10
        });

        Assert.Equal("durable-delta", result.CursorKind);
        Assert.Equal("https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages/delta?$deltatoken=next", result.NextCursor);
        Assert.True(result.SupportsDeletes);
        Assert.False(result.ResetRequired);
        Assert.Collection(result.Changes,
            item => {
                Assert.Equal(MailChangeKind.Upsert, item.Kind);
                Assert.Equal("up-1", item.MessageId);
                Assert.Equal("thread-1", item.ThreadId);
                Assert.Equal("hello", item.Subject);
            },
            item => {
                Assert.Equal(MailChangeKind.Delete, item.Kind);
                Assert.Equal("gone-1", item.MessageId);
            });
    }

    [Fact]
    public async Task GraphGoneReturnsResetEvidence() {
        var service = await CreateAsync(
            MailProfileKind.Graph,
            new HttpGraphSessionFactory(new RecordingHandler(Response(HttpStatusCode.Gone, "{}"))));

        var result = await service.GetChangesAsync(new MailChangeFeedRequest {
            ProfileId = "profile",
            Cursor = "https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages/delta?$deltatoken=expired"
        });

        Assert.True(result.ResetRequired);
        Assert.Empty(result.Changes);
        Assert.Equal("durable-delta", result.CursorKind);
    }

    [Theory]
    [InlineData("http://graph.microsoft.com/v1.0/delta")]
    [InlineData("https://example.test/v1.0/delta")]
    [InlineData("https://graph.microsoft.com.evil.test/v1.0/delta")]
    [InlineData("https://graph.microsoft.com:8443/v1.0/delta")]
    [InlineData("https://graph.microsoft.com/v1.0/me/messages")]
    [InlineData("https://graph.microsoft.com/v1.0/users/other/mailFolders/inbox/messages/delta?$deltatoken=x")]
    [InlineData("https://graph.microsoft.com/v1.0/me/mailFolders/archive/messages/delta?$deltatoken=x")]
    public async Task GraphCursorRejectsNonGraphDestinations(string cursor) {
        var service = await CreateAsync(MailProfileKind.Graph);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetChangesAsync(new MailChangeFeedRequest {
            ProfileId = "profile",
            Cursor = cursor
        }));
    }

    [Fact]
    public async Task GmailHistoryMapsChangesAndCursor() {
        const string body = "{\"historyId\":\"200\",\"history\":[{\"messagesAdded\":[{\"message\":{\"id\":\"up-1\"}}],\"messagesDeleted\":[{\"message\":{\"id\":\"gone-1\"}}]}]}";
        var service = await CreateAsync(
            MailProfileKind.Gmail,
            gmailFactory: new HttpGmailSessionFactory(new RecordingHandler(Response(HttpStatusCode.OK, body))));

        var result = await service.GetChangesAsync(new MailChangeFeedRequest {
            ProfileId = "profile",
            FolderId = "INBOX",
            Cursor = "100"
        });

        Assert.Equal("durable-history", result.CursorKind);
        Assert.Equal("200", result.NextCursor);
        Assert.True(result.SupportsDeletes);
        Assert.Contains(result.Changes, item => item.Kind == MailChangeKind.Upsert && item.MessageId == "up-1");
        Assert.Contains(result.Changes, item => item.Kind == MailChangeKind.Delete && item.MessageId == "gone-1");
    }

    [Fact]
    public async Task GmailHistoryCarriesProviderPageTokenWithoutSkippingEvents() {
        const string first = "{\"historyId\":\"200\",\"nextPageToken\":\"page-2\",\"history\":[{\"messagesAdded\":[{\"message\":{\"id\":\"up-1\"}}]}]}";
        const string second = "{\"historyId\":\"200\",\"history\":[{\"messagesAdded\":[{\"message\":{\"id\":\"up-2\"}}]}]}";
        var handler = new RecordingHandler(
            Response(HttpStatusCode.OK, first),
            Response(HttpStatusCode.OK, second));
        var service = await CreateAsync(
            MailProfileKind.Gmail,
            gmailFactory: new HttpGmailSessionFactory(handler));

        var firstResult = await service.GetChangesAsync(new MailChangeFeedRequest {
            ProfileId = "profile",
            FolderId = "INBOX",
            Cursor = "100",
            MaxChanges = 1
        });
        var secondResult = await service.GetChangesAsync(new MailChangeFeedRequest {
            ProfileId = "profile",
            FolderId = "INBOX",
            Cursor = firstResult.NextCursor,
            MaxChanges = 1
        });

        Assert.StartsWith("gmail-v1.100.", firstResult.NextCursor);
        Assert.Equal("up-1", Assert.Single(firstResult.Changes).MessageId);
        Assert.Equal("200", secondResult.NextCursor);
        Assert.Equal("up-2", Assert.Single(secondResult.Changes).MessageId);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("startHistoryId=100", handler.Requests[1].RequestUri!.Query);
        Assert.Contains("pageToken=page-2", handler.Requests[1].RequestUri!.Query);
    }

    [Fact]
    public async Task GmailExpiredHistoryReturnsResetEvidence() {
        var service = await CreateAsync(
            MailProfileKind.Gmail,
            gmailFactory: new HttpGmailSessionFactory(
                new RecordingHandler(Response(HttpStatusCode.NotFound, "{\"error\":\"stale\"}"))));

        var result = await service.GetChangesAsync(new MailChangeFeedRequest {
            ProfileId = "profile",
            Cursor = "100"
        });

        Assert.True(result.ResetRequired);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public async Task ProviderPageIsNeverTruncatedAfterCursorAdvances() {
        const string body = "{\"@odata.deltaLink\":\"https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages/delta?$deltatoken=next\",\"value\":[{\"id\":\"one\"},{\"id\":\"two\"}]}";
        var service = await CreateAsync(
            MailProfileKind.Graph,
            new HttpGraphSessionFactory(new RecordingHandler(Response(HttpStatusCode.OK, body))));

        var result = await service.GetChangesAsync(new MailChangeFeedRequest {
            ProfileId = "profile",
            MaxChanges = 1
        });

        Assert.Equal(2, result.Changes.Count);
    }

    [Fact]
    public async Task GmailMissingHistoryCursorIsRejectedBeforeConnecting() {
        var service = await CreateAsync(MailProfileKind.Gmail);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetChangesAsync(new MailChangeFeedRequest {
            ProfileId = "profile"
        }));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("12x")]
    [InlineData("https://example.test")]
    public async Task GmailCursorRejectsNonHistoryIdentifiers(string cursor) {
        var service = await CreateAsync(MailProfileKind.Gmail);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetChangesAsync(new MailChangeFeedRequest {
            ProfileId = "profile",
            Cursor = cursor
        }));
    }

    [Fact]
    public async Task GraphSubscribeMapsRemoteEvidence() {
        const string body = "{\"id\":\"sub-1\",\"resource\":\"me/mailFolders('inbox')/messages\",\"expirationDateTime\":\"2026-08-23T10:00:00Z\"}";
        var handler = new RecordingHandler(Response(HttpStatusCode.Created, body));
        var service = await CreateAsync(MailProfileKind.Graph, new HttpGraphSessionFactory(handler));

        var result = await service.SubscribeAsync(new MailChangeSubscriptionRequest {
            ProfileId = "profile",
            FolderIds = new List<string> { "INBOX" },
            NotificationUrl = "https://example.test/mail-hook",
            Expiration = new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero),
            ClientState = "opaque"
        });

        Assert.True(result.Succeeded);
        Assert.Equal("sub-1", result.SubscriptionId);
        Assert.Equal("me/mailFolders('inbox')/messages", result.Resource);
        Assert.Equal(new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero), result.Expiration);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
    }

    [Fact]
    public async Task GraphSubscribeRejectsMultipleFolderResources() {
        var handler = new RecordingHandler();
        var service = await CreateAsync(MailProfileKind.Graph, new HttpGraphSessionFactory(handler));

        await Assert.ThrowsAsync<ArgumentException>(() => service.SubscribeAsync(new MailChangeSubscriptionRequest {
            ProfileId = "profile",
            FolderIds = new List<string> { "INBOX", "Archive" },
            NotificationUrl = "https://example.test/mail-hook",
            Expiration = DateTimeOffset.UtcNow.AddHours(1)
        }));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GmailUnsubscribeRoutesToExplicitMailbox() {
        var handler = new RecordingHandler(Response(HttpStatusCode.NoContent, string.Empty));
        var service = await CreateAsync(
            MailProfileKind.Gmail,
            gmailFactory: new HttpGmailSessionFactory(handler),
            profile: new MailProfile {
                Id = "profile",
                DisplayName = "Profile",
                Kind = MailProfileKind.Gmail,
                DefaultMailbox = "default@example.test",
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [MailProfileSettingsKeys.Mailbox] = "configured@example.test"
                }
            });

        var result = await service.UnsubscribeAsync(new MailChangeUnsubscribeRequest {
            ProfileId = "profile",
            MailboxId = "override@example.test"
        });

        Assert.True(result.Succeeded);
        Assert.Equal("/gmail/v1/users/override@example.test/stop", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GmailWatchMapsLabelsCursorAndExpiration() {
        const string watch = "{\"historyId\":\"77\",\"expiration\":\"1787479200000\"}";
        var handler = new RecordingHandler(Response(HttpStatusCode.OK, watch));
        var service = await CreateAsync(
            MailProfileKind.Gmail,
            gmailFactory: new HttpGmailSessionFactory(handler));

        var result = await service.SubscribeAsync(new MailChangeSubscriptionRequest {
            ProfileId = "profile",
            FolderIds = new List<string> { "INBOX" },
            TopicName = "projects/test/topics/mail"
        });

        Assert.True(result.Succeeded);
        Assert.Equal("77", result.Cursor);
        Assert.NotNull(result.Expiration);
        Assert.Equal(new[] { "INBOX" }, result.FolderIds);
    }

    [Fact]
    public async Task Pop3ChangeFeedsAreReportedAsUnsupported() {
        var service = await CreateAsync(MailProfileKind.Pop3);

        await Assert.ThrowsAsync<NotSupportedException>(() => service.GetChangesAsync(new MailChangeFeedRequest {
            ProfileId = "profile"
        }));
    }

    private static async Task<MailChangeFeedService> CreateAsync(
        MailProfileKind kind,
        IGraphSessionFactory? graphFactory = null,
        IGmailSessionFactory? gmailFactory = null,
        MailProfile? profile = null) {
        var store = new InMemoryMailProfileStore();
        await store.SaveAsync(profile ?? new MailProfile {
            Id = "profile",
            DisplayName = "Profile",
            Kind = kind,
            DefaultMailbox = "me"
        });
        return new MailChangeFeedService(
            store,
            new ThrowingImapSessionFactory(),
            graphFactory ?? new ThrowingGraphSessionFactory(),
            gmailFactory ?? new ThrowingGmailSessionFactory());
    }

    private static HttpResponseMessage Response(HttpStatusCode statusCode, string body) =>
        new(statusCode) { Content = new StringContent(body) };

    private sealed class HttpGraphSessionFactory : IGraphSessionFactory {
        private readonly HttpMessageHandler _handler;

        public HttpGraphSessionFactory(HttpMessageHandler handler) => _handler = handler;

        public Task<GraphSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) {
            var userId = ResolveUserId(profile);
            var client = new HttpClient(_handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
            var api = new GraphApiClient(client, credential: new OAuthCredential {
                UserName = userId,
                AccessToken = "token",
                ExpiresOn = DateTimeOffset.MaxValue
            });
            return Task.FromResult(new GraphSession(api, userId));
        }
    }

    private sealed class HttpGmailSessionFactory : IGmailSessionFactory {
        private readonly HttpMessageHandler _handler;

        public HttpGmailSessionFactory(HttpMessageHandler handler) => _handler = handler;

        public Task<GmailSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) {
            var userId = ResolveUserId(profile);
            var client = new HttpClient(_handler) { BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/") };
            return Task.FromResult(new GmailSession(new GmailApiClient(client), userId));
        }
    }

    private static string ResolveUserId(MailProfile profile) =>
        profile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var mailbox) && !string.IsNullOrWhiteSpace(mailbox)
            ? mailbox.Trim()
            : profile.DefaultMailbox ?? "me";

    private sealed class ThrowingImapSessionFactory : IImapSessionFactory {
        public Task<ImapClient> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Unexpected IMAP connection.");
    }

    private sealed class ThrowingGraphSessionFactory : IGraphSessionFactory {
        public Task<GraphSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Unexpected Graph connection.");
    }

    private sealed class ThrowingGmailSessionFactory : IGmailSessionFactory {
        public Task<GmailSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Unexpected Gmail connection.");
    }
}
