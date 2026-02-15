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

    private static GraphApiClient CreateClient(HttpMessageHandler handler) {
        var api = new GraphApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = DateTimeOffset.MaxValue });
        var field = typeof(GraphApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(api, new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") });
        return api;
    }
}
