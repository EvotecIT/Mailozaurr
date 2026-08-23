using System.Net;
using System.Net.Http;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class JmapApiClientTests {
    [Fact]
    public async Task QueryEmails_DiscoversSessionAndSendsBoundedTypedMethodCall() {
        var handler = new RecordingHandler(
            JsonResponse(SessionJson("https://mail.example.test/jmap/api")),
            JsonResponse("{\"methodResponses\":[[\"Email/query\",{\"accountId\":\"a1\",\"queryState\":\"q1\",\"position\":0,\"ids\":[\"e1\"],\"total\":2},\"c1\"]]}"));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "secret-token", httpClient, callerOwnedClientDisablesRedirects: true);

        var result = await client.QueryEmailsAsync(new JmapEmailFilter { Text = "invoice" }, limit: 1);

        Assert.Equal("e1", Assert.Single(result.Ids));
        Assert.True(result.HasMore);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization!.Scheme);
        Assert.Equal("secret-token", handler.Requests[0].Headers.Authorization!.Parameter);
        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"Email/query\"", body, StringComparison.Ordinal);
        Assert.Contains("\"text\":\"invoice\"", body, StringComparison.Ordinal);
        Assert.Contains("\"limit\":1", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryEmails_PreservesNegativePositionFromEnd() {
        var handler = new RecordingHandler(
            JsonResponse(SessionJson("https://mail.example.test/jmap/api")),
            JsonResponse("{\"methodResponses\":[[\"Email/query\",{\"accountId\":\"a1\",\"queryState\":\"q1\",\"position\":1,\"ids\":[\"e2\"],\"total\":2},\"c1\"]]}"));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "secret-token", httpClient, callerOwnedClientDisablesRedirects: true);

        await client.QueryEmailsAsync(position: -1, limit: 1);

        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"position\":-1", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetEmails_EnforcesDiscoveredServerObjectLimit() {
        var handler = new RecordingHandler(JsonResponse(SessionJson("https://mail.example.test/jmap/api")));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "secret-token", httpClient, callerOwnedClientDisablesRedirects: true);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.GetEmailsAsync(new[] { "e1", "e2", "e3" }));

        Assert.Contains("2", exception.Message, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetThreads_EnforcesDiscoveredServerObjectLimit() {
        var handler = new RecordingHandler(JsonResponse(SessionJson("https://mail.example.test/jmap/api")));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "secret-token", httpClient, callerOwnedClientDisablesRedirects: true);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.GetThreadsAsync(new[] { "t1", "t2", "t3" }));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Session_RejectsCrossOriginApiUrlBeforeSendingBearerTokenThere() {
        var handler = new RecordingHandler(JsonResponse(SessionJson("https://attacker.example/jmap/api")));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "secret-token", httpClient, callerOwnedClientDisablesRedirects: true);

        var exception = await Assert.ThrowsAsync<JmapApiException>(() => client.GetSessionAsync());

        Assert.Equal("crossOriginApiUrl", exception.ErrorType);
        Assert.Single(handler.Requests);
        Assert.Equal("mail.example.test", handler.Requests[0].RequestUri!.Host);
    }

    [Fact]
    public async Task MethodError_IsSanitizedAndDoesNotExposeProviderPayload() {
        var handler = new RecordingHandler(
            JsonResponse(SessionJson("https://mail.example.test/jmap/api")),
            JsonResponse("{\"methodResponses\":[[\"error\",{\"type\":\"forbidden\",\"description\":\"secret provider text\"},\"c1\"]]}"));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "secret-token", httpClient, callerOwnedClientDisablesRedirects: true);

        var exception = await Assert.ThrowsAsync<JmapApiException>(() => client.ListMailboxesAsync());

        Assert.Equal("forbidden", exception.ErrorType);
        Assert.DoesNotContain("secret provider text", exception.Message, StringComparison.Ordinal);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK) {
        Content = new StringContent(json)
    };

    private static string SessionJson(string apiUrl) =>
        "{\"capabilities\":{\"urn:ietf:params:jmap:core\":{\"maxObjectsInGet\":2},\"urn:ietf:params:jmap:mail\":{}}," +
        "\"accounts\":{\"a1\":{\"name\":\"Primary\",\"isPersonal\":true,\"isReadOnly\":false," +
        "\"accountCapabilities\":{\"urn:ietf:params:jmap:mail\":{}}}}," +
        "\"primaryAccounts\":{\"urn:ietf:params:jmap:mail\":\"a1\"}," +
        "\"username\":\"user@example.test\",\"apiUrl\":\"" + apiUrl + "\",\"state\":\"s1\"}";
}
