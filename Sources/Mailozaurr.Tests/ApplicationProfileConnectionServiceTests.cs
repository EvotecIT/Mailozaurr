using MailKit.Net.Imap;
using Mailozaurr;

namespace Mailozaurr.Tests;

public sealed class ApplicationProfileConnectionServiceTests {
    [Fact]
    public async Task TestAsyncUsesMailboxScopeByDefaultForGmail() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "gmail-work",
                DisplayName = "Work Gmail",
                Kind = MailProfileKind.Gmail,
                DefaultMailbox = "user@gmail.com"
            }
        });
        var authProbeCalls = 0;
        var mailboxProbeCalls = 0;
        var service = new MailProfileConnectionService(
            profileStore,
            gmailSessionFactory: new FakeGmailSessionFactory(),
            probeGmailAsync: (_, _) => {
                authProbeCalls++;
                return Task.CompletedTask;
            },
            probeGmailMailboxAsync: (session, _) => {
                mailboxProbeCalls++;
                Assert.Equal("user@gmail.com", session.UserId);
                return Task.CompletedTask;
            });

        var result = await service.TestAsync("gmail-work");

        Assert.True(result.Succeeded);
        Assert.Equal(MailProfileConnectionTestScope.Auto, result.RequestedScope);
        Assert.Equal(MailProfileConnectionTestScope.Mailbox, result.ExecutedScope);
        Assert.Equal("listFolders", result.Probe);
        Assert.Equal(0, authProbeCalls);
        Assert.Equal(1, mailboxProbeCalls);
    }

    [Fact]
    public async Task TestAsyncUsesAuthScopeWhenRequestedForGraph() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "graph-work",
                DisplayName = "Work Graph",
                Kind = MailProfileKind.Graph,
                DefaultMailbox = "user@example.com"
            }
        });
        var authProbeCalls = 0;
        var mailboxProbeCalls = 0;
        var service = new MailProfileConnectionService(
            profileStore,
            graphSessionFactory: new FakeGraphSessionFactory(),
            probeGraphAsync: (_, _) => {
                authProbeCalls++;
                return Task.CompletedTask;
            },
            probeGraphMailboxAsync: (_, _) => {
                mailboxProbeCalls++;
                return Task.CompletedTask;
            });

        var result = await service.TestAsync("graph-work", MailProfileConnectionTestScope.Auth);

        Assert.True(result.Succeeded);
        Assert.Equal(MailProfileConnectionTestScope.Auth, result.RequestedScope);
        Assert.Equal(MailProfileConnectionTestScope.Auth, result.ExecutedScope);
        Assert.Equal("connect", result.Probe);
        Assert.Equal(1, authProbeCalls);
        Assert.Equal(0, mailboxProbeCalls);
    }

    [Fact]
    public async Task TestAsyncFallsBackFromSendToAuthForImap() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "imap-work",
                DisplayName = "Work IMAP",
                Kind = MailProfileKind.Imap,
                DefaultMailbox = "user@example.com"
            }
        });
        var service = new MailProfileConnectionService(
            profileStore,
            imapSessionFactory: new FakeImapSessionFactory(),
            probeImapAsync: (_, _) => Task.CompletedTask,
            probeImapMailboxAsync: (_, _) => Task.CompletedTask);

        var result = await service.TestAsync("imap-work", MailProfileConnectionTestScope.Send);

        Assert.True(result.Succeeded);
        Assert.Equal(MailProfileConnectionTestScope.Send, result.RequestedScope);
        Assert.Equal(MailProfileConnectionTestScope.Auth, result.ExecutedScope);
        Assert.Equal("connect", result.Probe);
    }

    private sealed class InMemoryProfileStore : IMailProfileStore {
        private readonly Dictionary<string, MailProfile> _profiles;

        public InMemoryProfileStore(IEnumerable<MailProfile> profiles) {
            _profiles = profiles.ToDictionary(profile => profile.Id, CloneProfile, StringComparer.OrdinalIgnoreCase);
        }

        public Task<IReadOnlyList<MailProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MailProfile>>(_profiles.Values.Select(CloneProfile).ToArray());

        public Task<MailProfile?> GetByIdAsync(string profileId, CancellationToken cancellationToken = default) {
            _profiles.TryGetValue(profileId, out var profile);
            return Task.FromResult(profile == null ? null : CloneProfile(profile));
        }

        public Task<bool> RemoveAsync(string profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_profiles.Remove(profileId));

        public Task SaveAsync(MailProfile profile, CancellationToken cancellationToken = default) {
            _profiles[profile.Id] = CloneProfile(profile);
            return Task.CompletedTask;
        }

        private static MailProfile CloneProfile(MailProfile profile) => new() {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            Description = profile.Description,
            Kind = profile.Kind,
            DefaultSender = profile.DefaultSender,
            DefaultMailbox = profile.DefaultMailbox,
            IsDefault = profile.IsDefault,
            Settings = new Dictionary<string, string>(profile.Settings, StringComparer.OrdinalIgnoreCase),
            Capabilities = profile.Capabilities == null
                ? null
                : new ProfileCapabilities(profile.Capabilities.Kind, profile.Capabilities.Capabilities)
        };
    }

    private sealed class FakeImapSessionFactory : IImapSessionFactory {
        public Task<ImapClient> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ImapClient());
    }

    private sealed class FakeGraphSessionFactory : IGraphSessionFactory {
        public Task<GraphSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GraphSession(new GraphApiClient(new OAuthCredential {
                UserName = profile.DefaultMailbox ?? "me",
                AccessToken = "token",
                ExpiresOn = DateTimeOffset.MaxValue
            }), profile.DefaultMailbox ?? "me"));
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
