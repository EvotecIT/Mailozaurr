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
    public async System.Threading.Tasks.Task ListConversationMessagesAsync_BuildsFilterQuery() {
        var json = "{\"value\":[{\"id\":\"m1\",\"subject\":\"s\"}]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var api = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(api, new HttpClient(handler) { BaseAddress = new System.Uri("https://graph.microsoft.com/v1.0/") });

        var msgs = await api.ListConversationMessagesAsync("conv-1");
        Assert.Single(msgs);
        Assert.Single(handler.Requests);
        var uri = handler.Requests[0].RequestUri!.ToString();
        Assert.Contains("/me/messages?", uri);
        Assert.Contains("$filter=", uri);
        Assert.Contains("conversationId", uri);
        Assert.Contains("conv-1", uri);
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

    [Fact]
    public async System.Threading.Tasks.Task CreateMessageAsync_CreatesDraftInFolder() {
        var json = "{\"id\":\"m-created\",\"subject\":\"s\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json)
        });
        var api = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(api, new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") });

        var created = await api.CreateMessageAsync(
            new GraphMessage {
                Subject = "s",
                Body = new GraphContent { Type = "Text", Content = "body" }
            },
            folderIdOrWellKnownName: "sentitems");

        Assert.Equal("m-created", created.Id);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("/me/mailFolders/sentitems/messages", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task SendDraftMessageAsync_PostsToSendEndpoint() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Accepted) {
            Content = new StringContent(string.Empty)
        });
        var api = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(api, new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") });

        await api.SendDraftMessageAsync("m123");

        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("/me/messages/m123/send", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task CreateAttachmentUploadSessionAsync_UsesAttachmentItemEnvelope() {
        var json = "{\"uploadUrl\":\"https://upload.example/session\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json)
        });
        var api = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(api, new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") });

        var session = await api.CreateAttachmentUploadSessionAsync(
            "m123",
            new GraphAttachmentItem("file", "a.txt", 10));

        Assert.Equal("https://upload.example/session", session.UploadUrl);
        Assert.Single(handler.Requests);
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"attachmentItem\"", body);
        Assert.Contains("\"attachmentType\":\"file\"", body);
        Assert.Contains("\"name\":\"a.txt\"", body);
        Assert.Contains("\"size\":10", body);
    }

    [Fact]
    public async System.Threading.Tasks.Task BatchSetMessagesIsReadAsync_ReturnsPerMessageStatus() {
        var batchResponse = "{\"responses\":[{\"id\":\"1\",\"status\":200,\"headers\":{},\"body\":{}},{\"id\":\"2\",\"status\":400,\"headers\":{},\"body\":{\"error\":{\"message\":\"bad-request\"}}}]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(batchResponse)
        });
        var api = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(api, new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") });

        var results = await api.BatchSetMessagesIsReadAsync(new[] { "m-1", "m-2" }, isRead: true);

        Assert.Equal(2, results.Count);
        Assert.True(results[0].Ok);
        Assert.Equal("m-1", results[0].Id);
        Assert.False(results[1].Ok);
        Assert.Equal("m-2", results[1].Id);
        Assert.Contains("bad-request", results[1].Error);

        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("/$batch", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"url\":\"me/messages/m-1\"", body);
        Assert.Contains("\"isRead\":true", body);
    }

    [Fact]
    public async System.Threading.Tasks.Task BatchMoveConversationsAsync_ListsConversationMessages_ThenMovesInBatch() {
        var listResponse = "{\"value\":[{\"id\":\"m-1\"},{\"id\":\"m-2\"}]}";
        var batchResponse = "{\"responses\":[{\"id\":\"1\",\"status\":201,\"headers\":{},\"body\":{}},{\"id\":\"2\",\"status\":201,\"headers\":{},\"body\":{}}]}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(listResponse) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(batchResponse) });
        var api = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(api, new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") });

        var results = await api.BatchMoveConversationsAsync(new[] { "conv-1" }, destinationFolderId: "archive");

        Assert.Single(results);
        Assert.Equal("conv-1", results[0].Id);
        Assert.True(results[0].Ok);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Contains("conversationId", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"url\":\"me/messages/m-1/move\"", body);
        Assert.Contains("\"url\":\"me/messages/m-2/move\"", body);
        Assert.Contains("\"destinationId\":\"archive\"", body);
    }

    [Fact]
    public async System.Threading.Tasks.Task BatchDeleteMessagesAsync_WhenBatchFails_ReturnsFailurePerMessage() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError) {
            Content = new StringContent("{\"error\":{\"message\":\"batch-down\"}}")
        });
        var api = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(api, new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") });

        var results = await api.BatchDeleteMessagesAsync(new[] { "m-1", "m-2" });

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.False(r.Ok));
        Assert.Contains("Graph batch failed (500).", results[0].Error);
        Assert.Contains("Graph batch failed (500).", results[1].Error);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("/$batch", handler.Requests[0].RequestUri!.ToString());
    }
}