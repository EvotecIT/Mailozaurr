using System.Net;
using System.Net.Http;
using System.Reflection;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphApiClientMailboxTests {
    [Fact]
    public async System.Threading.Tasks.Task ListMessagesAsync_AddsConsistencyHeaders_WhenSearchIsUsed() {
        var json = "{\"value\":[{\"id\":\"m1\",\"subject\":\"s\",\"receivedDateTime\":\"2026-02-15T00:00:00Z\",\"internetMessageId\":\"<x>\",\"hasAttachments\":false,\"isRead\":true,\"conversationId\":\"c1\",\"from\":{\"emailAddress\":{\"address\":\"a@b.com\"}},\"toRecipients\":[{\"emailAddress\":{\"address\":\"c@d.com\"}}],\"flag\":{\"flagStatus\":\"flagged\"}}]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var api = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(api, new HttpClient(handler) { BaseAddress = new System.Uri("https://graph.microsoft.com/v1.0/") });

        var page = await api.ListMessagesAsync("inbox", search: "hello");
        Assert.Single(page.Items);
        Assert.Equal("m1", page.Items[0].Id);
        Assert.Equal("a@b.com", page.Items[0].From!.Email.Address);

        Assert.Single(handler.Requests);
        Assert.True(handler.Requests[0].Headers.Contains("ConsistencyLevel"));
        Assert.True(handler.Requests[0].Headers.Contains("Prefer"));
        Assert.Contains("/me/mailFolders/inbox/messages?", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task DeltaMessagesAsync_ParsesDeletes_AndCursor() {
        var json = "{\"@odata.deltaLink\":\"https://graph.microsoft.com/v1.0/delta\",\"value\":[{\"id\":\"d1\",\"@removed\":{}},{\"id\":\"u1\",\"subject\":\"x\"}]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var api = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(api, new HttpClient(handler) { BaseAddress = new System.Uri("https://graph.microsoft.com/v1.0/") });

        var delta = await api.DeltaMessagesAsync("inbox", cursor: null, top: 5);
        Assert.Equal("https://graph.microsoft.com/v1.0/delta", delta.Cursor);
        Assert.Single(delta.DeletedIds);
        Assert.Equal("d1", delta.DeletedIds[0]);
        Assert.Single(delta.Items);
        Assert.Equal("u1", delta.Items[0].Id);
    }

    [Fact]
    public async System.Threading.Tasks.Task SendBatchAsync_UsesBatchEndpoint_AndSerializesRelativeUrls() {
        var json = "{\"responses\":[{\"id\":\"1\",\"status\":204,\"headers\":{},\"body\":{}}]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var api = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(api, new HttpClient(handler) { BaseAddress = new System.Uri("https://graph.microsoft.com/v1.0/") });

        var r = new GraphBatchRequest { Id = "", Method = GraphHttpMethod.DELETE, Url = "/me/messages/123" };
        var results = await api.SendBatchAsync(new[] { r });
        Assert.Single(results);
        Assert.Equal(204, results[0].Status);

        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/$batch", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"url\":\"me/messages/123\"", body);
        Assert.Contains("\"method\":\"DELETE\"", body);
    }
}

