using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphMailboxBrowserTests {
    [Theory]
    [InlineData(null, "inbox")]
    [InlineData("", "inbox")]
    [InlineData("INBOX", "inbox")]
    [InlineData("Sent Items", "sentitems")]
    [InlineData("Drafts", "drafts")]
    [InlineData("Archive", "archive")]
    [InlineData("Junk", "junkemail")]
    [InlineData("Spam", "junkemail")]
    [InlineData("Trash", "deleteditems")]
    [InlineData("my-folder-id", "my-folder-id")]
    public void ResolveFolderSelector_MapsKnownAliases(string? input, string expected) {
        var actual = GraphMailboxBrowser.ResolveFolderSelector(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListFoldersAsync_BuildsHierarchicalNames() {
        var topLevelJson = "{" +
                           "\"value\":[" +
                           "{\"id\":\"inbox-id\",\"displayName\":\"Inbox\",\"childFolderCount\":1,\"wellKnownName\":\"inbox\",\"totalItemCount\":10,\"unreadItemCount\":2}," +
                           "{\"id\":\"archive-id\",\"displayName\":\"Archive\",\"childFolderCount\":0,\"wellKnownName\":\"archive\"}" +
                           "]" +
                           "}";
        var childJson = "{" +
                        "\"value\":[" +
                        "{\"id\":\"projects-id\",\"displayName\":\"Projects\",\"parentFolderId\":\"inbox-id\",\"childFolderCount\":0}" +
                        "]" +
                        "}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(topLevelJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(childJson) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var folders = await browser.ListFoldersAsync();

        Assert.Equal(3, folders.Count);
        Assert.Equal("Archive", folders[0].Name);
        Assert.Equal("Inbox", folders[1].Name);
        Assert.Equal("Inbox/Projects", folders[2].Name);
        Assert.Equal("inbox", folders[1].WellKnownName);
        Assert.Equal(10, folders[1].TotalItemCount);
        Assert.Equal(2, folders[1].UnreadItemCount);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/me/mailFolders?$top=200", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("/me/mailFolders/inbox-id/childFolders?$top=200", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task ListMessagesAsync_ReturnsTotalCount_AndMappedSummaries() {
        var folderJson = "{\"id\":\"inbox\",\"totalItemCount\":12}";
        var listJson = "{\"value\":[{\"id\":\"m1\",\"subject\":\"s\",\"receivedDateTime\":\"2026-02-15T00:00:00Z\",\"internetMessageId\":\"<msg@example.test>\",\"hasAttachments\":true,\"isRead\":false,\"conversationId\":\"conv-1\",\"from\":{\"emailAddress\":{\"address\":\"a@example.test\"}},\"toRecipients\":[{\"emailAddress\":{\"address\":\"b@example.test\"}}],\"flag\":{\"flagStatus\":\"flagged\"}}]}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(folderJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(listJson) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var result = await browser.ListMessagesAsync("INBOX", limit: 50, offset: 10);

        Assert.Equal("inbox", result.FolderSelector);
        Assert.Equal(12, result.TotalCount);
        Assert.Single(result.Messages);
        Assert.Equal("m1", result.Messages[0].NativeId);
        Assert.Equal("msg@example.test", result.Messages[0].MessageId);
        Assert.True(result.Messages[0].HasAttachments);
        Assert.False(result.Messages[0].Seen);
        Assert.True(result.Messages[0].Flagged);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/me/mailFolders/inbox?$select=totalItemCount", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("/me/mailFolders/inbox/messages?", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task SearchMessagesAsync_UsesGraphSearchWhenTextIsProvided() {
        var listJson = "{\"value\":[{\"id\":\"m1\",\"subject\":\"hello\"}]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(listJson) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var result = await browser.SearchMessagesAsync(new GraphMailboxBrowser.GraphMailboxSearchRequest {
            Folder = "Archive",
            Query = "urgent report",
            UnseenOnly = true,
            HasAttachment = true
        }, max: 25);

        Assert.Equal("archive", result.FolderSelector);
        Assert.Single(result.Messages);
        Assert.Single(handler.Requests);
        var uri = handler.Requests[0].RequestUri!.ToString();
        Assert.Contains("/me/mailFolders/archive/messages?", uri);
        Assert.Contains("$search=", uri);
        Assert.DoesNotContain("$filter=", uri);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeltaMessagesAsync_MapsUpsertsDeletesAndCursor() {
        var json = "{\"@odata.deltaLink\":\"https://graph.microsoft.com/v1.0/delta\",\"value\":[{\"id\":\"m-up\",\"subject\":\"hello\"},{\"id\":\"m-del\",\"@removed\":{}}]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var result = await browser.DeltaMessagesAsync("INBOX", cursor: null, max: 100);

        Assert.Equal("inbox", result.FolderSelector);
        Assert.Equal("https://graph.microsoft.com/v1.0/delta", result.Cursor);
        Assert.Single(result.Upserts);
        Assert.Single(result.DeletedNativeIds);
        Assert.Equal("m-up", result.Upserts[0].NativeId);
        Assert.Equal("m-del", result.DeletedNativeIds[0]);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetMessageContentAsync_ReturnsMimeAndFlags() {
        var metaJson = "{\"id\":\"m1\",\"isRead\":true,\"flag\":{\"flagStatus\":\"flagged\"}}";
        var mime = "From: a@example.test\r\nTo: b@example.test\r\nSubject: Sample\r\nMessage-Id: <m1@example.test>\r\n\r\nhello";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(metaJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(mime)) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var result = await browser.GetMessageContentAsync("m1");

        Assert.True(result.Seen);
        Assert.True(result.Flagged);
        Assert.Equal("Sample", result.Message.Subject);
        Assert.Equal(2, handler.Requests.Count);
        var metaUri = handler.Requests[0].RequestUri!.ToString();
        Assert.Contains("/me/messages/m1?", metaUri);
        Assert.Contains("$select=", metaUri);
        Assert.Contains("isRead", metaUri);
        Assert.Contains("flag", metaUri);
        Assert.Contains("/me/messages/m1/$value", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task SetMessageSeenAsync_PatchesReadState() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        await browser.SetMessageSeenAsync("m1", seen: true);

        Assert.Single(handler.Requests);
        var request = handler.Requests[0];
        Assert.Equal(new HttpMethod("PATCH"), request.Method);
        Assert.Contains("/me/messages/m1", request.RequestUri!.ToString());
        var body = await request.Content!.ReadAsStringAsync();
        Assert.Contains("\"isRead\":true", body, StringComparison.Ordinal);
    }

    [Fact]
    public async System.Threading.Tasks.Task SetMessageFlaggedAsync_PatchesFlagState() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        await browser.SetMessageFlaggedAsync("m1", flagged: true);

        Assert.Single(handler.Requests);
        var request = handler.Requests[0];
        Assert.Equal(new HttpMethod("PATCH"), request.Method);
        Assert.Contains("/me/messages/m1", request.RequestUri!.ToString());
        var body = await request.Content!.ReadAsStringAsync();
        Assert.Contains("\"flagStatus\":\"flagged\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async System.Threading.Tasks.Task MoveMessageAsync_ResolvesFolderAliasAndUsesDestinationId() {
        var folderJson = "{\"id\":\"archive-id\"}";
        var movedJson = "{\"id\":\"m1\"}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(folderJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(movedJson) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        await browser.MoveMessageAsync("m1", "Archive");

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/me/mailFolders/archive?$select=id", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("/me/messages/m1/move", handler.Requests[1].RequestUri!.ToString());
        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"destinationId\":\"archive-id\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteMessageAsync_UsesDeleteEndpoint() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.NoContent) { Content = new StringContent(string.Empty) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        await browser.DeleteMessageAsync("m1");

        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Contains("/me/messages/m1", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task MoveMessagesAsync_ResolvesFolderAliasAndBatchesMoveRequests() {
        var folderJson = "{\"id\":\"archive-id\"}";
        var batchJson = "{\"responses\":[{\"id\":\"1\",\"status\":201},{\"id\":\"2\",\"status\":201}]}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(folderJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(batchJson) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var results = await browser.MoveMessagesAsync(new[] { "m1", "m2" }, "Archive");

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Ok, r.Error));
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/me/mailFolders/archive?$select=id", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("/$batch", handler.Requests[1].RequestUri!.ToString());
        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("me/messages/m1/move", body, StringComparison.Ordinal);
        Assert.Contains("me/messages/m2/move", body, StringComparison.Ordinal);
        Assert.Contains("\"destinationId\":\"archive-id\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteConversationsAsync_ExpandsAndDeletesConversationMessages() {
        var listJson = "{\"value\":[{\"id\":\"m1\"},{\"id\":\"m2\"}]}";
        var batchJson = "{\"responses\":[{\"id\":\"1\",\"status\":204},{\"id\":\"2\",\"status\":204}]}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(listJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(batchJson) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var results = await browser.DeleteConversationsAsync(new[] { "conv-1" });

        Assert.Single(results);
        Assert.True(results[0].Ok, results[0].Error);
        Assert.Equal("conv-1", results[0].Id);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/me/messages?", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("conversationId", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("/$batch", handler.Requests[1].RequestUri!.ToString());
        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("me/messages/m1", body, StringComparison.Ordinal);
        Assert.Contains("me/messages/m2", body, StringComparison.Ordinal);
    }

    private static GraphApiClient CreateClient(HttpMessageHandler handler) {
        var api = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(api, new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") });
        return api;
    }
}
