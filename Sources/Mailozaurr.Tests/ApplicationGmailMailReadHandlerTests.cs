using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationGmailMailReadHandlerTests {
    [Fact]
    public async Task HandlerUsesInjectedFolderDelegate() {
        var handler = new GmailMailReadHandler(
            new FakeGmailSessionFactory(),
            getFoldersAsync: (session, profile, query, cancellationToken) => Task.FromResult<IReadOnlyList<FolderRef>>(new[] {
                new FolderRef {
                    ProfileId = profile.Id,
                    MailboxId = session.UserId,
                    Id = "INBOX",
                    DisplayName = "Inbox",
                    Path = "Inbox"
                }
            }));

        var results = await handler.GetFoldersAsync(
            new MailProfile {
                Id = "personal-gmail",
                DisplayName = "Personal Gmail",
                Kind = MailProfileKind.Gmail,
                DefaultMailbox = "me"
            },
            new MailFolderQuery {
                ProfileId = "personal-gmail"
            });

        Assert.Single(results);
        Assert.Equal("INBOX", results[0].Id);
    }

    [Fact]
    public async Task HandlerUsesInjectedSearchDelegate() {
        var handler = new GmailMailReadHandler(
            new FakeGmailSessionFactory(),
            searchAsync: (session, profile, request, cancellationToken) => Task.FromResult<IReadOnlyList<MessageSummary>>(new[] {
                new MessageSummary {
                    ProfileId = profile.Id,
                    Id = "gmail-42",
                    Subject = request.QueryText
                }
            }));

        var results = await handler.SearchAsync(
            new MailProfile {
                Id = "personal-gmail",
                DisplayName = "Personal Gmail",
                Kind = MailProfileKind.Gmail,
                DefaultMailbox = "me"
            },
            new MailSearchRequest {
                ProfileId = "personal-gmail",
                QueryText = "reports"
            });

        Assert.Single(results);
        Assert.Equal("gmail-42", results[0].Id);
        Assert.Equal("reports", results[0].Subject);
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