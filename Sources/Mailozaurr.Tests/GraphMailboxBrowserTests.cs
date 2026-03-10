using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using MimeKit;
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
    public async System.Threading.Tasks.Task ListConversationMessagesPageAsync_ReturnsPagedSortedSummaries() {
        var listJson = "{\"value\":[" +
                       "{\"id\":\"m1\",\"subject\":\"first\",\"receivedDateTime\":\"2026-02-14T00:00:00Z\",\"conversationId\":\"conv-1\"}," +
                       "{\"id\":\"m2\",\"subject\":\"second\",\"receivedDateTime\":\"2026-02-15T00:00:00Z\",\"conversationId\":\"conv-1\"}" +
                       "]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(listJson) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var page = await browser.ListConversationMessagesPageAsync("conv-1", limit: 1, offset: 1);

        Assert.Equal("conv-1", page.ConversationId);
        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Messages);
        Assert.Equal("m1", page.Messages[0].NativeId);
        Assert.Single(handler.Requests);
        Assert.Contains("/me/messages?", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("conversationId", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task ImportMessageAsync_CreatesMessageInResolvedFolder_AndReturnsResult() {
        var createdJson = "{\"id\":\"created-id\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(createdJson) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.test"));
        message.To.Add(MailboxAddress.Parse("recipient@example.test"));
        message.Subject = "Imported";
        message.MessageId = "<imported@example.test>";
        message.Headers.Replace("X-Idempotency-Key", "idem-123");
        message.Body = new TextPart("plain") { Text = "hello" };

        var result = await browser.ImportMessageAsync(
            message,
            folder: "Sent Items",
            idempotencyHeaderName: "X-Idempotency-Key");

        Assert.Equal("sentitems", result.FolderSelector);
        Assert.Equal("created-id", result.NativeId);
        Assert.Equal("imported@example.test", result.MessageId);

        Assert.Single(handler.Requests);
        Assert.Contains("/me/mailFolders/sentitems/messages", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"subject\":\"Imported\"", body, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"X-Idempotency-Key\"", body, StringComparison.Ordinal);
        Assert.Contains("\"value\":\"idem-123\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async System.Threading.Tasks.Task ImportMessageAsync_UploadsLargeAttachmentsThroughUploadSession() {
        var createJson = "{\"id\":\"created-id\"}";
        var uploadSessionJson = "{\"uploadUrl\":\"https://upload.test/session\"}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(createJson) },
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(uploadSessionJson) },
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(string.Empty) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.test"));
        message.To.Add(MailboxAddress.Parse("recipient@example.test"));
        message.Subject = "With attachment";
        message.MessageId = "<upload@example.test>";
        var builder = new BodyBuilder { TextBody = "hello" };
        var attachmentBytes = Encoding.UTF8.GetBytes("hello world");
        builder.Attachments.Add("notes.txt", attachmentBytes);
        message.Body = builder.ToMessageBody();

        var result = await browser.ImportMessageAsync(
            message,
            folder: "Sent Items",
            maxInlineAttachmentBytes: 0);

        Assert.Equal("sentitems", result.FolderSelector);
        Assert.Equal("created-id", result.NativeId);
        Assert.Equal("upload@example.test", result.MessageId);

        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("/me/mailFolders/sentitems/messages", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("/me/messages/created-id/attachments/createUploadSession", handler.Requests[1].RequestUri!.ToString());
        Assert.Equal("https://upload.test/session", handler.Requests[2].RequestUri!.ToString());
        var uploadBody = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"name\":\"notes.txt\"", uploadBody, StringComparison.Ordinal);
        Assert.Contains("\"size\":11", uploadBody, StringComparison.Ordinal);
        var range = handler.Requests[2].Content!.Headers.ContentRange;
        Assert.NotNull(range);
        Assert.Equal(0L, range!.From);
        Assert.Equal(10L, range.To);
        Assert.Equal(11L, range.Length);
    }

    [Fact]
    public async System.Threading.Tasks.Task SendMessageAsync_CreatesDraftAndSendsIt() {
        var createJson = "{\"id\":\"draft-id\"}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(createJson) },
            new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent(string.Empty) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.test"));
        message.To.Add(MailboxAddress.Parse("recipient@example.test"));
        message.Subject = "Send me";
        message.MessageId = "<send@example.test>";
        message.Headers.Replace("X-Idempotency-Key", "idem-send");
        message.Body = new TextPart("plain") { Text = "hello" };

        var result = await browser.SendMessageAsync(
            message,
            idempotencyHeaderName: "X-Idempotency-Key");

        Assert.Equal("draft-id", result.DraftId);
        Assert.Equal("send@example.test", result.MessageId);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/me/mailFolders/drafts/messages", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("/me/messages/draft-id/send", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task FindMessageByInternetMessageIdAsync_UsesBracketedFilterAndReturnsMatch() {
        var listJson = "{\"value\":[{\"id\":\"m1\",\"internetMessageId\":\"<msg-123@example.test>\"}]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(listJson) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var result = await browser.FindMessageByInternetMessageIdAsync("msg-123@example.test", folder: "Sent Items");

        Assert.True(result.IsMatch);
        Assert.Equal("sentitems", result.FolderSelector);
        Assert.Equal("m1", result.NativeId);
        Assert.Equal("msg-123@example.test", result.MessageId);
        Assert.Single(handler.Requests);
        var requestUri = handler.Requests[0].RequestUri!;
        Assert.Contains("/me/mailFolders/sentitems/messages?", requestUri.ToString());
        var decodedQuery = Uri.UnescapeDataString(requestUri.Query);
        Assert.Contains("internetMessageId eq '<msg-123@example.test>'", decodedQuery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("internetMessageId eq 'msg-123@example.test'", decodedQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async System.Threading.Tasks.Task FindMessageByInternetMessageIdAsync_ReturnsNoMatch_WhenResponseContainsDifferentMessageId() {
        var listJson = "{\"value\":[{\"id\":\"m1\",\"internetMessageId\":\"<other@example.test>\"}]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(listJson) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var result = await browser.FindMessageByInternetMessageIdAsync("msg-123@example.test", folder: "Sent Items");

        Assert.False(result.IsMatch);
        Assert.Equal("sentitems", result.FolderSelector);
        Assert.Null(result.NativeId);
        Assert.Null(result.MessageId);
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
    public async System.Threading.Tasks.Task GetThreadingMetadataAsync_ParsesHeaderFieldsFromMime() {
        var mime = "From: a@example.test\r\n" +
                   "To: b@example.test\r\n" +
                   "Reply-To: replies@example.test\r\n" +
                   "Cc: c@example.test\r\n" +
                   "Message-Id: <thread-child@example.test>\r\n" +
                   "In-Reply-To: <thread-parent@example.test>\r\n" +
                   "References: <thread-root@example.test> <thread-parent@example.test> <thread-root@example.test>\r\n" +
                   "\r\nhello";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(mime)) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var result = await browser.GetThreadingMetadataAsync("m1");

        Assert.Equal("thread-child@example.test", result.MessageId);
        Assert.Equal("thread-parent@example.test", result.InReplyTo);
        Assert.Equal("replies@example.test", result.ReplyTo);
        Assert.Equal("c@example.test", result.Cc);
        Assert.Equal(2, result.References.Count);
        Assert.Equal("thread-root@example.test", result.References[0]);
        Assert.Equal("thread-parent@example.test", result.References[1]);
        Assert.Single(handler.Requests);
        Assert.Contains("/me/messages/m1/$value", handler.Requests[0].RequestUri!.ToString());
    }

    [Theory]
    [InlineData("Archive", "me/mailFolders('archive')/messages")]
    [InlineData("AAMkADk0Y2Qx", "me/mailFolders('AAMkADk0Y2Qx')/messages")]
    [InlineData("A'B", "me/mailFolders('A''B')/messages")]
    public void BuildMessageSubscriptionResource_MapsAndEscapesFolderSelector(string folder, string expected) {
        var actual = GraphMailboxBrowser.BuildMessageSubscriptionResource(folder);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async System.Threading.Tasks.Task CreateMessageSubscriptionAsync_PostsSubscriptionPayloadAndMapsResult() {
        var json = "{\"id\":\"sub-1\",\"resource\":\"me/mailFolders('archive')/messages\",\"clientState\":\"state-1\",\"expirationDateTime\":\"2026-02-16T01:00:00Z\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(json) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);
        var expiration = new DateTimeOffset(new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc));

        var result = await browser.CreateMessageSubscriptionAsync(
            notificationUrl: "https://example.test/webhook",
            folder: "Archive",
            expirationDateTime: expiration,
            changeType: "created,updated,deleted",
            clientState: "state-1");

        Assert.Equal("sub-1", result.SubscriptionId);
        Assert.Equal("me/mailFolders('archive')/messages", result.Resource);
        Assert.Equal("state-1", result.ClientState);
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 2, 16, 1, 0, 0, DateTimeKind.Utc)), result.ExpirationDateTime);
        Assert.Single(handler.Requests);
        Assert.Contains("/subscriptions", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        Assert.Equal("https://example.test/webhook", doc.RootElement.GetProperty("notificationUrl").GetString());
        Assert.Equal("me/mailFolders('archive')/messages", doc.RootElement.GetProperty("resource").GetString());
        Assert.Equal("created,updated,deleted", doc.RootElement.GetProperty("changeType").GetString());
    }

    [Fact]
    public async System.Threading.Tasks.Task RenewSubscriptionAsync_PatchesSubscriptionAndMapsResult() {
        var json = "{\"id\":\"sub-renew\",\"resource\":\"me/mailFolders('inbox')/messages\",\"clientState\":\"state-2\",\"expirationDateTime\":\"2026-02-16T01:00:00Z\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);
        var expiration = new DateTimeOffset(new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc));

        var result = await browser.RenewSubscriptionAsync("sub-renew", expiration);

        Assert.Equal("sub-renew", result.SubscriptionId);
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 2, 16, 1, 0, 0, DateTimeKind.Utc)), result.ExpirationDateTime);
        Assert.Single(handler.Requests);
        Assert.Equal(new HttpMethod("PATCH"), handler.Requests[0].Method);
        Assert.Contains("/subscriptions/sub-renew", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task RenewSubscriptionSafeAsync_ReturnsMissing_WhenSubscriptionIsGone() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"missing\"}") });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);
        var expiration = new DateTimeOffset(new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc));

        var result = await browser.RenewSubscriptionSafeAsync("sub-renew-missing", expiration, treatMissingAsStale: true);

        Assert.False(result.Renewed);
        Assert.True(result.Missing);
        Assert.Null(result.Subscription);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteSubscriptionAsync_ReturnsAlreadyDeleted_WhenMissingAndConfigured() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"missing\"}") });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var result = await browser.DeleteSubscriptionAsync("sub-missing", treatMissingAsSuccess: true);

        Assert.True(result.Deleted);
        Assert.True(result.AlreadyDeleted);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Contains("/subscriptions/sub-missing", handler.Requests[0].RequestUri!.ToString());
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
    public async System.Threading.Tasks.Task ArchiveMessageAsync_UsesArchiveDestinationAlias() {
        var folderJson = "{\"id\":\"archive-id\"}";
        var movedJson = "{\"id\":\"m1\"}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(folderJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(movedJson) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        await browser.ArchiveMessageAsync("m1");

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/me/mailFolders/archive?$select=id", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("/me/messages/m1/move", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task TrashMessageAsync_UsesTrashDestinationAlias() {
        var folderJson = "{\"id\":\"trash-id\"}";
        var movedJson = "{\"id\":\"m1\"}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(folderJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(movedJson) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        await browser.TrashMessageAsync("m1");

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/me/mailFolders/deleteditems?$select=id", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("/me/messages/m1/move", handler.Requests[1].RequestUri!.ToString());
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
    public async System.Threading.Tasks.Task MoveMessagesAsync_WithNoMessageIds_SkipsFolderResolution() {
        var handler = new RecordingHandler();
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var results = await browser.MoveMessagesAsync(Array.Empty<string>(), "Archive");

        Assert.Empty(results);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async System.Threading.Tasks.Task ArchiveConversationsAsync_UsesArchiveDestinationAlias() {
        var folderJson = "{\"id\":\"archive-id\"}";
        var listJson = "{\"value\":[{\"id\":\"m1\"}]}";
        var batchJson = "{\"responses\":[{\"id\":\"1\",\"status\":201}]}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(folderJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(listJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(batchJson) });
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var results = await browser.ArchiveConversationsAsync(new[] { "conv-1" });

        Assert.Single(results);
        Assert.All(results, r => Assert.True(r.Ok, r.Error));
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("/me/mailFolders/archive?$select=id", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("/me/messages?", handler.Requests[1].RequestUri!.ToString());
        Assert.Contains("conversationId", handler.Requests[1].RequestUri!.ToString());
        Assert.Contains("/$batch", handler.Requests[2].RequestUri!.ToString());
        var body = await handler.Requests[2].Content!.ReadAsStringAsync();
        Assert.Contains("me/messages/m1/move", body, StringComparison.Ordinal);
        Assert.Contains("\"destinationId\":\"archive-id\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async System.Threading.Tasks.Task MoveConversationsAsync_WithNoConversationIds_SkipsFolderResolution() {
        var handler = new RecordingHandler();
        var client = CreateClient(handler);
        var browser = new GraphMailboxBrowser(client);

        var results = await browser.MoveConversationsAsync(Array.Empty<string>(), "Archive");

        Assert.Empty(results);
        Assert.Empty(handler.Requests);
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
