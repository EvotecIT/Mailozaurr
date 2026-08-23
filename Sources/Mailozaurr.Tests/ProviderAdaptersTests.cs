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

    [Fact]
    public async Task CliJmapEmailQuery_PassesRequestedPositionToTheSharedService() {
        var store = await CreateStoreAsync(MailProfileKind.Jmap);
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(
                    "{\"capabilities\":{\"urn:ietf:params:jmap:core\":{\"maxObjectsInGet\":100,\"maxSizeRequest\":1000000},\"urn:ietf:params:jmap:mail\":{}}," +
                    "\"accounts\":{\"a1\":{\"accountCapabilities\":{\"urn:ietf:params:jmap:mail\":{}}}}," +
                    "\"primaryAccounts\":{\"urn:ietf:params:jmap:mail\":\"a1\"},\"apiUrl\":\"https://mail.example.test/jmap/api\",\"state\":\"s1\"}")
            },
            new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(
                    "{\"methodResponses\":[[\"Email/query\",{\"accountId\":\"a1\",\"queryState\":\"q1\",\"position\":4,\"ids\":[\"e5\"],\"total\":6},\"c1\"]]}")
            });
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliRunner.RunAsync(
            new[] { "provider", "jmap-email-query", "--profile", "profile", "--position", "4", "--limit", "1" },
            output,
            error,
            _ => CreateBuilder(
                store,
                new GraphFactory(new RecordingHandler()),
                new GmailFactory(new RecordingHandler()),
                new JmapFactory(handler)));

        Assert.Equal(0, exitCode);
        Assert.Contains("position=4", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("hasMore=True", output.ToString(), StringComparison.Ordinal);
        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"position\":4", body, StringComparison.Ordinal);
    }

    private static MailApplicationBuilder CreateBuilder(
        InMemoryMailProfileStore store,
        IGraphSessionFactory graph,
        IGmailSessionFactory gmail,
        IJmapSessionFactory? jmap = null) {
        var builder = new MailApplicationBuilder()
            .UseProfileStore(store)
            .UseSecretStore(new InMemoryMailSecretStore())
            .UseGraphSessionFactory(graph)
            .UseGmailSessionFactory(gmail);
        return jmap == null ? builder : builder.UseJmapSessionFactory(jmap);
    }

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

    private sealed class JmapFactory : IJmapSessionFactory {
        private readonly HttpMessageHandler _handler;
        internal JmapFactory(HttpMessageHandler handler) => _handler = handler;
        public Task<JmapSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new JmapSession(new JmapApiClient(
                new Uri("https://mail.example.test/.well-known/jmap"),
                "token",
                new HttpClient(_handler),
                callerOwnedClientDisablesRedirects: true)));
    }
}
#endif
