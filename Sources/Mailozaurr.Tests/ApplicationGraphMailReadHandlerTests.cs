using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationGraphMailReadHandlerTests {
    [Fact]
    public async Task HandlerUsesInjectedFolderDelegate() {
        var handler = new GraphMailReadHandler(
            new FakeGraphSessionFactory(),
            getFoldersAsync: (session, profile, query, cancellationToken) => Task.FromResult<IReadOnlyList<FolderRef>>(new[] {
                new FolderRef {
                    ProfileId = profile.Id,
                    MailboxId = query.MailboxId ?? session.UserId,
                    Id = "inbox",
                    DisplayName = "Inbox",
                    Path = "Inbox"
                }
            }));

        var results = await handler.GetFoldersAsync(
            new MailProfile {
                Id = "work-graph",
                DisplayName = "Work Graph",
                Kind = MailProfileKind.Graph,
                DefaultMailbox = "user@example.com"
            },
            new MailFolderQuery {
                ProfileId = "work-graph"
            });

        Assert.Single(results);
        Assert.Equal("inbox", results[0].Id);
        Assert.Equal("Inbox", results[0].DisplayName);
    }

    [Fact]
    public async Task HandlerUsesInjectedSearchDelegate() {
        var handler = new GraphMailReadHandler(
            new FakeGraphSessionFactory(),
            searchAsync: (session, profile, request, cancellationToken) => Task.FromResult<IReadOnlyList<MessageSummary>>(new[] {
                new MessageSummary {
                    ProfileId = profile.Id,
                    Id = "graph-42",
                    Subject = request.QueryText
                }
            }));

        var results = await handler.SearchAsync(
            new MailProfile {
                Id = "work-graph",
                DisplayName = "Work Graph",
                Kind = MailProfileKind.Graph,
                DefaultMailbox = "user@example.com"
            },
            new MailSearchRequest {
                ProfileId = "work-graph",
                QueryText = "reports"
            });

        Assert.Single(results);
        Assert.Equal("graph-42", results[0].Id);
        Assert.Equal("reports", results[0].Subject);
    }

    private sealed class FakeGraphSessionFactory : IGraphSessionFactory {
        public Task<GraphSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GraphSession(new GraphApiClient(new OAuthCredential {
                UserName = profile.DefaultMailbox ?? "me",
                AccessToken = "token",
                ExpiresOn = DateTimeOffset.MaxValue
            }), profile.DefaultMailbox ?? "me"));
    }
}
