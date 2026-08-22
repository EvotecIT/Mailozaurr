using System.Net;
using System.Net.Http;
using System.Text;
using Xunit;

namespace Mailozaurr.Tests;

public class ProviderMailboxApplicationServicesTests {
    [Fact]
    public async Task GraphService_UsesProfileAndMailboxOverride() {
        var store = await CreateStoreAsync(MailProfileKind.Graph);
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"value\":[{\"id\":\"r1\",\"displayName\":\"Rule\"}]}")
        });
        var factory = new GraphFactory(handler);
        var service = new GraphMailboxService(store, factory);

        var rules = await service.ListRulesAsync("profile", mailboxId: "shared+mailbox@example.test");

        Assert.Equal("r1", Assert.Single(rules).Id);
        Assert.Equal("shared+mailbox@example.test", factory.LastProfile!.DefaultMailbox);
        Assert.Equal("shared+mailbox@example.test", factory.LastProfile.Settings[MailProfileSettingsKeys.Mailbox]);
        Assert.Contains("users/shared%2Bmailbox%40example.test/mailFolders/inbox/messageRules", Assert.Single(handler.Requests).RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GmailService_RejectsWrongProviderBeforeConnecting() {
        var store = await CreateStoreAsync(MailProfileKind.Graph);
        var factory = new GmailFactory(new RecordingHandler());
        var service = new GmailMailboxService(store, factory);

        await Assert.ThrowsAsync<NotSupportedException>(() => service.ListFiltersAsync("profile"));

        Assert.Null(factory.LastProfile);
    }

    [Fact]
    public async Task GmailService_ReturnsBoundedThreadPage() {
        var store = await CreateStoreAsync(MailProfileKind.Gmail);
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"threads\":[{\"id\":\"t1\"}],\"nextPageToken\":\"next\"}")
        });
        var service = new GmailMailboxService(store, new GmailFactory(handler));

        var page = await service.ListThreadsAsync("profile", pageSize: 1);

        Assert.Equal("t1", Assert.Single(page.Threads).Id);
        Assert.Equal("next", page.NextPageToken);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PermissionEvidence_ReportsGraphTokenClaimsAsNonAuthoritative() {
        var store = await CreateStoreAsync(MailProfileKind.Graph);
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"id\":\"object-1\",\"displayName\":\"User\",\"mail\":\"user@example.test\",\"userPrincipalName\":\"user@example.test\"}")
        });
        var graphFactory = new GraphFactory(handler, CreateJwt("{\"scp\":\"Mail.ReadWrite Calendars.ReadWrite\",\"preferred_username\":\"user@example.test\",\"oid\":\"object-1\"}"));
        var service = new MailPermissionEvidenceService(store, graphFactory, new GmailFactory(new RecordingHandler()));

        var result = await service.GetEvidenceAsync("profile");

        Assert.True(result.ProbeSucceeded);
        Assert.Equal("user@example.test", result.Identity!.EmailAddress);
        Assert.False(result.Permissions!.Authoritative);
        Assert.Contains("Mail.ReadWrite", result.Permissions.DelegatedScopes);
        Assert.Contains("user@example.test", result.Permissions.DelegatedMailboxIdentifiers);
    }

    [Fact]
    public async Task PermissionEvidence_ReturnsSafeProviderFailureWithoutResponseBody() {
        var store = await CreateStoreAsync(MailProfileKind.Graph);
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Forbidden) {
            Content = new StringContent("secret-provider-payload")
        });
        var service = new MailPermissionEvidenceService(store, new GraphFactory(handler), new GmailFactory(new RecordingHandler()));

        var result = await service.GetEvidenceAsync("profile");

        Assert.False(result.ProbeSucceeded);
        Assert.Equal("graph_403", result.FailureCode);
        Assert.DoesNotContain("secret-provider-payload", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FeatureScopeBundlesAreExplicitAndDefaultsRemainNarrower() {
        Assert.DoesNotContain("https://graph.microsoft.com/MailboxSettings.ReadWrite", MailProfileAuthDefaults.GraphScopes);
        Assert.DoesNotContain("https://graph.microsoft.com/Calendars.ReadWrite", MailProfileAuthDefaults.GraphScopes);
        Assert.Contains("https://graph.microsoft.com/MailboxSettings.ReadWrite", MailProfileAuthDefaults.GraphMailboxFeatureScopes);
        Assert.Contains("https://graph.microsoft.com/Calendars.ReadWrite", MailProfileAuthDefaults.GraphMailboxFeatureScopes);

        Assert.DoesNotContain("https://www.googleapis.com/auth/gmail.settings.basic", MailProfileAuthDefaults.GmailScopes);
        Assert.Contains("https://www.googleapis.com/auth/gmail.settings.basic", MailProfileAuthDefaults.GmailMailboxFeatureScopes);
    }

    [Fact]
    public void CapabilityCatalog_DistinguishesEvidenceFromDelegationManagement() {
        var graph = MailCapabilityCatalog.For(MailProfileKind.Graph);
        var gmail = MailCapabilityCatalog.For(MailProfileKind.Gmail);

        Assert.True(graph.Supports(MailCapability.InspectPermissions));
        Assert.False(graph.Supports(MailCapability.ManagePermissions));
        Assert.True(graph.Supports(MailCapability.ManageRules | MailCapability.ManageEvents | MailCapability.UseThreads));
        Assert.True(gmail.Supports(MailCapability.ManageRules | MailCapability.UseLabels | MailCapability.UseThreads));
        Assert.False(gmail.Supports(MailCapability.ManageEvents | MailCapability.ManagePermissions));
    }

    private static async Task<InMemoryMailProfileStore> CreateStoreAsync(MailProfileKind kind) {
        var store = new InMemoryMailProfileStore();
        await store.SaveAsync(new MailProfile {
            Id = "profile",
            DisplayName = "Profile",
            Kind = kind,
            DefaultMailbox = "me"
        });
        return store;
    }

    private static string CreateJwt(string payload) {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return "header." + encoded + ".signature";
    }

    private sealed class GraphFactory : IGraphSessionFactory {
        private readonly HttpMessageHandler _handler;
        private readonly string _accessToken;

        internal GraphFactory(HttpMessageHandler handler, string accessToken = "token") {
            _handler = handler;
            _accessToken = accessToken;
        }

        internal MailProfile? LastProfile { get; private set; }

        public Task<GraphSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) {
            LastProfile = profile;
            var credential = new OAuthCredential { AccessToken = _accessToken, ExpiresOn = DateTimeOffset.MaxValue };
            var client = new GraphApiClient(
                new HttpClient(_handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") },
                credential: credential);
            return Task.FromResult(new GraphSession(client, profile.DefaultMailbox ?? "me", credential));
        }
    }

    private sealed class GmailFactory : IGmailSessionFactory {
        private readonly HttpMessageHandler _handler;

        internal GmailFactory(HttpMessageHandler handler) => _handler = handler;

        internal MailProfile? LastProfile { get; private set; }

        public Task<GmailSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) {
            LastProfile = profile;
            var client = new GmailApiClient(
                new HttpClient(_handler) { BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/") },
                credential: new OAuthCredential { AccessToken = "token", ExpiresOn = DateTimeOffset.MaxValue });
            return Task.FromResult(new GmailSession(client, profile.DefaultMailbox ?? "me"));
        }
    }
}
