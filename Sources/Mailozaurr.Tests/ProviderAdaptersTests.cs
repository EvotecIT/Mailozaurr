#if NET8_0_OR_GREATER
using Mailozaurr.Cli;
using Mailozaurr.Cli.Mcp;
using System.Net;
using System.Net.Http;
using Xunit;

namespace Mailozaurr.Tests;

public class ProviderAdaptersTests {
    [Fact]
    public async Task CliGraphRuleList_DelegatesToApplicationService() {
        var store = await CreateStoreAsync(MailProfileKind.Graph);
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"value\":[{\"id\":\"r1\",\"displayName\":\"Route invoices\"}]}")
        });
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliRunner.RunAsync(
            new[] { "provider", "graph-rule-list", "--profile", "profile", "--json" },
            output,
            error,
            _ => CreateBuilder(store, new GraphFactory(handler), new GmailFactory(new RecordingHandler())));

        Assert.Equal(0, exitCode);
        Assert.Contains("Route invoices", output.ToString(), StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(error.ToString()));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task CliGmailThreadList_ReturnsContinuationEvidence() {
        var store = await CreateStoreAsync(MailProfileKind.Gmail);
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"threads\":[{\"id\":\"t1\"}],\"nextPageToken\":\"next\"}")
        });
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliRunner.RunAsync(
            new[] { "provider", "gmail-thread-list", "--profile", "profile", "--limit", "1", "--json" },
            output,
            error,
            _ => CreateBuilder(store, new GraphFactory(new RecordingHandler()), new GmailFactory(handler)));

        Assert.Equal(0, exitCode);
        Assert.Contains("next", output.ToString(), StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task McpGraphRules_UsesSharedApplicationService() {
        var store = await CreateStoreAsync(MailProfileKind.Graph);
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"value\":[{\"id\":\"r1\"}]}")
        });
        var tools = new MailMcpTools(CreateBuilder(store, new GraphFactory(handler), new GmailFactory(new RecordingHandler())).Build());

        var rules = await tools.mail_graph_rule_list("profile");

        Assert.Equal("r1", Assert.Single(rules).Id);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task McpGmailLabels_UsesSharedApplicationService() {
        var store = await CreateStoreAsync(MailProfileKind.Gmail);
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"labels\":[{\"id\":\"L1\",\"name\":\"Projects\",\"type\":\"user\"}]}")
        });
        var tools = new MailMcpTools(CreateBuilder(store, new GraphFactory(new RecordingHandler()), new GmailFactory(handler)).Build());

        var labels = await tools.mail_gmail_label_list("profile");

        Assert.Equal("Projects", Assert.Single(labels).Name);
        Assert.Single(handler.Requests);
    }

    private static MailApplicationBuilder CreateBuilder(InMemoryMailProfileStore store, IGraphSessionFactory graph, IGmailSessionFactory gmail) =>
        new MailApplicationBuilder()
            .UseProfileStore(store)
            .UseSecretStore(new InMemoryMailSecretStore())
            .UseGraphSessionFactory(graph)
            .UseGmailSessionFactory(gmail);

    private static async Task<InMemoryMailProfileStore> CreateStoreAsync(MailProfileKind kind) {
        var store = new InMemoryMailProfileStore();
        await store.SaveAsync(new MailProfile { Id = "profile", DisplayName = "Profile", Kind = kind, DefaultMailbox = "me" });
        return store;
    }

    private sealed class GraphFactory : IGraphSessionFactory {
        private readonly HttpMessageHandler _handler;
        internal GraphFactory(HttpMessageHandler handler) => _handler = handler;
        public Task<GraphSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) {
            var credential = new OAuthCredential { AccessToken = "token", ExpiresOn = DateTimeOffset.MaxValue };
            return Task.FromResult(new GraphSession(new GraphApiClient(
                new HttpClient(_handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") },
                credential: credential), profile.DefaultMailbox ?? "me", credential));
        }
    }

    private sealed class GmailFactory : IGmailSessionFactory {
        private readonly HttpMessageHandler _handler;
        internal GmailFactory(HttpMessageHandler handler) => _handler = handler;
        public Task<GmailSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GmailSession(new GmailApiClient(
                new HttpClient(_handler) { BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/") },
                credential: new OAuthCredential { AccessToken = "token", ExpiresOn = DateTimeOffset.MaxValue }),
                profile.DefaultMailbox ?? "me"));
    }
}
#endif
