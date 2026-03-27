using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using MimeKit;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class GmailMailboxBrowserTests {
    [Fact]
    public async System.Threading.Tasks.Task ResolveLabelIdAsync_MapsSystemAliases_AndResolvesCustomLabel() {
        var labelsJson = "{\"labels\":[{\"id\":\"Label_1\",\"name\":\"Project\"}]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(labelsJson) });
        var browser = CreateBrowser(handler);

        Assert.Equal("INBOX", await browser.ResolveLabelIdAsync("INBOX"));
        Assert.Equal("SENT", await browser.ResolveLabelIdAsync("Sent Items"));
        Assert.Equal("Label_1", await browser.ResolveLabelIdAsync("Project"));

        Assert.Single(handler.Requests);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/labels?fields=labels(id,name,type)", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task ListFoldersAsync_ReturnsSortedLabelFolders() {
        var labelsJson = "{" +
                         "\"labels\":[" +
                         "{\"id\":\"Label_2\",\"name\":\"Zeta\",\"type\":\"user\"}," +
                         "{\"id\":\"INBOX\",\"name\":\"Inbox\",\"type\":\"system\"}," +
                         "{\"id\":\"Label_1\",\"name\":\"Alpha\",\"type\":\"user\"}" +
                         "]" +
                         "}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(labelsJson) });
        var browser = CreateBrowser(handler);

        var folders = await browser.ListFoldersAsync();

        Assert.Equal(3, folders.Count);
        Assert.Equal("Alpha", folders[0].Name);
        Assert.Equal("Inbox", folders[1].Name);
        Assert.Equal("Zeta", folders[2].Name);
        Assert.Equal("INBOX", folders[1].Id);
        Assert.Equal("system", folders[1].Type);
        Assert.Single(handler.Requests);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/labels?fields=labels(id,name,type)", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task ListMessagesAsync_UsesListThenLoadsSummaries() {
        var listJson = "{\"messages\":[{\"id\":\"m1\",\"threadId\":\"t1\"},{\"id\":\"m2\",\"threadId\":\"t1\"}],\"resultSizeEstimate\":\"9\"}";
        var m2Json = "{" +
                     "\"id\":\"m2\",\"threadId\":\"t1\",\"internalDate\":\"1739577600000\"," +
                     "\"labelIds\":[\"INBOX\"]," +
                     "\"payload\":{\"filename\":\"\",\"headers\":[" +
                     "{\"name\":\"From\",\"value\":\"a@example.test\"}," +
                     "{\"name\":\"To\",\"value\":\"b@example.test\"}," +
                     "{\"name\":\"Subject\",\"value\":\"second\"}," +
                     "{\"name\":\"Message-Id\",\"value\":\"<m2@example.test>\"}]}}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(listJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(m2Json) });
        var browser = CreateBrowser(handler);

        var result = await browser.ListMessagesAsync("INBOX", limit: 1, offset: 1);

        Assert.Equal("INBOX", result.ResolvedLabelId);
        Assert.Equal(9, result.TotalCount);
        Assert.Single(result.Messages);
        Assert.Equal("m2", result.Messages[0].NativeId);
        Assert.Equal("m2@example.test", result.Messages[0].MessageId);
        Assert.Equal("a@example.test", result.Messages[0].From);

        Assert.Equal(2, handler.Requests.Count);
        var listUri = handler.Requests[0].RequestUri!;
        Assert.Contains("/users/me/messages", listUri.ToString());
        var listQuery = ParseQueryParams(listUri);
        Assert.Equal("messages(id,threadId),nextPageToken,resultSizeEstimate", Assert.Single(listQuery["fields"]));
        Assert.Equal("INBOX", Assert.Single(listQuery["labelIds"]));

        var summaryUri = handler.Requests[1].RequestUri!.ToString();
        Assert.Contains("/users/me/messages/m2?", summaryUri);
        Assert.Contains("format=full", summaryUri);
    }

    [Fact]
    public async System.Threading.Tasks.Task SearchMessagesAsync_UsesBuiltQuery_AndSortsByDateDesc() {
        var listJson = "{\"messages\":[{\"id\":\"m-old\"},{\"id\":\"m-new\"}],\"nextPageToken\":null}";
        var oldJson = "{" +
                      "\"id\":\"m-old\",\"threadId\":\"t1\",\"internalDate\":\"1739491200000\"," +
                      "\"payload\":{\"headers\":[{\"name\":\"Subject\",\"value\":\"old\"}]}}";
        var newJson = "{" +
                      "\"id\":\"m-new\",\"threadId\":\"t1\",\"internalDate\":\"1739577600000\"," +
                      "\"payload\":{\"headers\":[{\"name\":\"Subject\",\"value\":\"new\"}]}}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(listJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(oldJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(newJson) });
        var browser = CreateBrowser(handler);

        var search = await browser.SearchMessagesAsync(new GmailMailboxBrowser.GmailMailboxSearchRequest {
            Folder = "INBOX",
            Query = "urgent",
            SubjectContains = "invoice",
            UnseenOnly = true
        }, max: 20);

        Assert.Equal("INBOX", search.ResolvedLabelId);
        Assert.Equal(2, search.Messages.Count);
        Assert.Equal("m-new", search.Messages[0].NativeId);
        Assert.Equal("m-old", search.Messages[1].NativeId);

        Assert.Equal(3, handler.Requests.Count);
        var uri = handler.Requests[0].RequestUri!;
        var query = ParseQueryParams(uri);
        Assert.Equal("INBOX", Assert.Single(query["labelIds"]));
        var q = Assert.Single(query["q"]);
        Assert.Contains("is:unread", q);
        Assert.Contains("subject:(invoice)", q);
        Assert.Contains("urgent", q);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListThreadMessagesAsync_ReturnsSortedSummaries() {
        var threadJson = "{" +
                         "\"id\":\"thr-1\",\"messages\":[" +
                         "{\"id\":\"m1\",\"threadId\":\"thr-1\",\"internalDate\":\"1739491200000\",\"payload\":{\"headers\":[{\"name\":\"Subject\",\"value\":\"first\"}]}}," +
                         "{\"id\":\"m2\",\"threadId\":\"thr-1\",\"internalDate\":\"1739577600000\",\"payload\":{\"headers\":[{\"name\":\"Subject\",\"value\":\"second\"}]}}" +
                         "]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(threadJson) });
        var browser = CreateBrowser(handler);

        var messages = await browser.ListThreadMessagesAsync("thr-1");

        Assert.Equal(2, messages.Count);
        Assert.Equal("m2", messages[0].NativeId);
        Assert.Equal("m1", messages[1].NativeId);
        Assert.Single(handler.Requests);
        Assert.Contains("/users/me/threads/thr-1", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task ListThreadMessagesPageAsync_ReturnsPagedSortedSummaries() {
        var threadJson = "{" +
                         "\"id\":\"thr-1\",\"messages\":[" +
                         "{\"id\":\"m1\",\"threadId\":\"thr-1\",\"internalDate\":\"1739491200000\",\"payload\":{\"headers\":[{\"name\":\"Subject\",\"value\":\"first\"}]}}," +
                         "{\"id\":\"m2\",\"threadId\":\"thr-1\",\"internalDate\":\"1739577600000\",\"payload\":{\"headers\":[{\"name\":\"Subject\",\"value\":\"second\"}]}}" +
                         "]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(threadJson) });
        var browser = CreateBrowser(handler);

        var page = await browser.ListThreadMessagesPageAsync("thr-1", limit: 1, offset: 1);

        Assert.Equal("thr-1", page.ThreadId);
        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Messages);
        Assert.Equal("m1", page.Messages[0].NativeId);
        Assert.Single(handler.Requests);
        Assert.Contains("/users/me/threads/thr-1", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task GetMessageContentAsync_ReturnsMimeAndFlags() {
        var mime = "From: a@example.test\r\nTo: b@example.test\r\nSubject: Sample\r\nMessage-Id: <m1@example.test>\r\n\r\nhello";
        var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(mime)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var rawJson = "{\"id\":\"m1\",\"threadId\":\"thr-1\",\"labelIds\":[\"UNREAD\",\"STARRED\"],\"raw\":\"" + raw + "\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(rawJson) });
        var browser = CreateBrowser(handler);

        var result = await browser.GetMessageContentAsync("m1");

        Assert.NotNull(result.Message);
        Assert.Equal("Sample", result.Message.Subject);
        Assert.False(result.Seen);
        Assert.True(result.Flagged);
        Assert.Equal("thr-1", result.NativeThreadId);
    }

    [Fact]
    public async System.Threading.Tasks.Task SendMessageAsync_UsesGmailSendEndpoint() {
        var sentJson = "{\"id\":\"gmail-sent-id\",\"threadId\":\"gmail-thread-id\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(sentJson) });
        var browser = CreateBrowser(handler);
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.test"));
        message.To.Add(MailboxAddress.Parse("recipient@example.test"));
        message.Subject = "Send me";
        message.Body = new TextPart("plain") { Text = "hello" };

        var result = await browser.SendMessageAsync(message);

        Assert.Equal("gmail-sent-id", result.NativeId);
        Assert.Equal("gmail-thread-id", result.NativeThreadId);
        Assert.Single(handler.Requests);
        Assert.Contains("/users/me/messages/send", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"raw\":\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetThreadingMetadataAsync_ParsesSelectedHeaders() {
        var json = "{" +
                   "\"id\":\"m1\"," +
                   "\"payload\":{\"headers\":[" +
                   "{\"name\":\"Message-ID\",\"value\":\"<thread-child@example.test>\"}," +
                   "{\"name\":\"In-Reply-To\",\"value\":\"<thread-parent@example.test>\"}," +
                   "{\"name\":\"References\",\"value\":\"<thread-root@example.test> <thread-parent@example.test> <thread-root@example.test>\"}," +
                   "{\"name\":\"Reply-To\",\"value\":\"replies@example.test\"}," +
                   "{\"name\":\"Cc\",\"value\":\"cc@example.test\"}" +
                   "]}}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var browser = CreateBrowser(handler);

        var result = await browser.GetThreadingMetadataAsync("m1");

        Assert.Equal("thread-child@example.test", result.MessageId);
        Assert.Equal("thread-parent@example.test", result.InReplyTo);
        Assert.Equal("replies@example.test", result.ReplyTo);
        Assert.Equal("cc@example.test", result.Cc);
        Assert.Equal(2, result.References.Count);
        Assert.Equal("thread-root@example.test", result.References[0]);
        Assert.Equal("thread-parent@example.test", result.References[1]);

        Assert.Single(handler.Requests);
        var uri = handler.Requests[0].RequestUri!;
        var query = ParseQueryParams(uri);
        Assert.Equal("metadata", Assert.Single(query["format"]));
        Assert.Equal("id,payload(headers)", Assert.Single(query["fields"]));
        Assert.Contains("Message-ID", query["metadataHeaders"]);
        Assert.Contains("In-Reply-To", query["metadataHeaders"]);
        Assert.Contains("References", query["metadataHeaders"]);
        Assert.Contains("Reply-To", query["metadataHeaders"]);
        Assert.Contains("Cc", query["metadataHeaders"]);
    }

    [Fact]
    public async System.Threading.Tasks.Task WatchAsync_ResolvesLabels_AndParsesExpiration() {
        var labelsJson = "{\"labels\":[{\"id\":\"Label_1\",\"name\":\"Project\"}]}";
        var watchJson = "{\"historyId\":\"77\",\"expiration\":\"1739577600000\"}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(labelsJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(watchJson) });
        var browser = CreateBrowser(handler);

        var result = await browser.WatchAsync("projects/p/topics/t", new[] { "INBOX", "Project" });

        Assert.Equal("77", result.HistoryId);
        Assert.NotNull(result.ExpirationUtc);
        Assert.Contains("INBOX", result.LabelIds);
        Assert.Contains("Label_1", result.LabelIds);

        Assert.Equal(2, handler.Requests.Count);
        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"topicName\":\"projects/p/topics/t\"", body);
        Assert.Contains("INBOX", body);
        Assert.Contains("Label_1", body);
    }

    [Fact]
    public async System.Threading.Tasks.Task StopWatchAsync_ReturnsAlreadyStopped_WhenMissingAndConfigured() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"missing\"}") });
        var browser = CreateBrowser(handler);

        var result = await browser.StopWatchAsync(treatMissingAsSuccess: true);

        Assert.True(result.Stopped);
        Assert.True(result.AlreadyStopped);
        Assert.Single(handler.Requests);
        Assert.Contains("/users/me/stop", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task StopWatchAsync_Throws_WhenMissingAndStrictMode() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"missing\"}") });
        var browser = CreateBrowser(handler);

        await Assert.ThrowsAsync<GmailApiException>(() => browser.StopWatchAsync(treatMissingAsSuccess: false));
    }

    [Fact]
    public async System.Threading.Tasks.Task GetHistoryAsync_MapsUpsertsDeletes_AndDropsDeletedFromUpserts() {
        var historyJson = "{" +
                          "\"historyId\":\"200\"," +
                          "\"history\":[" +
                          "{" +
                          "\"id\":\"10\"," +
                          "\"messagesAdded\":[{\"message\":{\"id\":\"m1\"}},{\"message\":{\"id\":\"m2\"}}]," +
                          "\"messagesDeleted\":[{\"message\":{\"id\":\"m2\"}}]," +
                          "\"labelsAdded\":[{\"message\":{\"id\":\"m3\"}}]," +
                          "\"labelsRemoved\":[{\"message\":{\"id\":\"m3\"}}]" +
                          "}" +
                          "]" +
                          "}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(historyJson) });
        var browser = CreateBrowser(handler);

        var result = await browser.GetHistoryAsync("INBOX", "5", maxChanges: 100);

        Assert.Equal("INBOX", result.ResolvedLabelId);
        Assert.Equal("200", result.NewHistoryId);
        Assert.Equal(new[] { "m1" }, result.UpsertNativeIds);
        Assert.Equal(new[] { "m2", "m3" }, result.DeletedNativeIds);

        Assert.Single(handler.Requests);
        var uri = handler.Requests[0].RequestUri!;
        var query = ParseQueryParams(uri);
        Assert.Equal("5", Assert.Single(query["startHistoryId"]));
        Assert.Equal("INBOX", Assert.Single(query["labelId"]));
        Assert.Contains("messageAdded", query["historyTypes"]);
        Assert.Contains("messageDeleted", query["historyTypes"]);
        Assert.Contains("labelAdded", query["historyTypes"]);
        Assert.Contains("labelRemoved", query["historyTypes"]);
    }

    [Fact]
    public void BuildSearchQuery_ComposesExpectedTokens() {
        var query = GmailMailboxBrowser.BuildSearchQuery(new GmailMailboxBrowser.GmailMailboxSearchRequest {
            Query = "urgent",
            SubjectContains = "invoice",
            FromContains = "boss@example.test",
            ToContains = "team@example.test",
            BodyContains = "overdue",
            UnseenOnly = true,
            HasAttachment = true,
            SinceUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            BeforeUtc = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        });

        Assert.Contains("is:unread", query);
        Assert.Contains("has:attachment", query);
        Assert.Contains("after:1704067200", query);
        Assert.Contains("before:1704153600", query);
        Assert.Contains("subject:(invoice)", query);
        Assert.Contains("from:(boss@example.test)", query);
        Assert.Contains("to:(team@example.test)", query);
        Assert.Contains("overdue", query);
        Assert.Contains("urgent", query);
    }

    [Fact]
    public async System.Threading.Tasks.Task SetMessageSeenAsync_ModifiesMessageLabels() {
        var modifyJson = "{\"id\":\"m1\",\"threadId\":\"t1\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(modifyJson) });
        var browser = CreateBrowser(handler);

        await browser.SetMessageSeenAsync("m1", seen: true);

        Assert.Single(handler.Requests);
        Assert.Contains("/users/me/messages/m1/modify", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"removeLabelIds\":[\"UNREAD\"]", body, StringComparison.Ordinal);
    }

    [Fact]
    public async System.Threading.Tasks.Task MoveMessageAsync_ResolvesLabels_AndModifiesMessage() {
        var labelsJson = "{\"labels\":[{\"id\":\"Label_Target\",\"name\":\"Archive\"}]}";
        var modifyJson = "{\"id\":\"m1\",\"threadId\":\"t1\"}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(labelsJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(modifyJson) });
        var browser = CreateBrowser(handler);

        await browser.MoveMessageAsync("m1", sourceFolder: "INBOX", targetFolder: "Archive");

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/users/me/labels", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("/users/me/messages/m1/modify", handler.Requests[1].RequestUri!.ToString());
        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"addLabelIds\":[\"Label_Target\"]", body, StringComparison.Ordinal);
        Assert.Contains("INBOX", body, StringComparison.Ordinal);
        Assert.Contains("TRASH", body, StringComparison.Ordinal);
    }

    [Fact]
    public async System.Threading.Tasks.Task MoveMessageAsync_ToTrash_UsesTrashEndpoint() {
        var trashJson = "{\"id\":\"m1\",\"threadId\":\"t1\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(trashJson) });
        var browser = CreateBrowser(handler);

        await browser.MoveMessageAsync("m1", sourceFolder: "INBOX", targetFolder: "TRASH");

        Assert.Single(handler.Requests);
        Assert.Contains("/users/me/messages/m1/trash", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task MoveMessageAsync_WithNullSourceFolder_DoesNotRemoveInbox() {
        var labelsJson = "{\"labels\":[{\"id\":\"Label_Target\",\"name\":\"Archive\"}]}";
        var modifyJson = "{\"id\":\"m1\",\"threadId\":\"t1\"}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(labelsJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(modifyJson) });
        var browser = CreateBrowser(handler);

        await browser.MoveMessageAsync("m1", sourceFolder: null, targetFolder: "Archive");

        Assert.Equal(2, handler.Requests.Count);
        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"addLabelIds\":[\"Label_Target\"]", body, StringComparison.Ordinal);
        Assert.Contains("\"removeLabelIds\":[\"TRASH\"]", body, StringComparison.Ordinal);
        Assert.DoesNotContain("INBOX", body, StringComparison.Ordinal);
    }

    [Fact]
    public async System.Threading.Tasks.Task ArchiveMessagesAsync_UsesBatchModify_AndReturnsPerMessageResults() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) });
        var browser = CreateBrowser(handler);

        var results = await browser.ArchiveMessagesAsync(new[] { "m1", "m2" });

        Assert.Equal(2, results.Count);
        Assert.All(results, x => Assert.True(x.Ok, x.Error));
        Assert.Single(handler.Requests);
        Assert.Contains("/users/me/messages/batchModify", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"ids\":[\"m1\",\"m2\"]", body, StringComparison.Ordinal);
        Assert.Contains("\"removeLabelIds\":[\"INBOX\",\"TRASH\"]", body, StringComparison.Ordinal);
    }

    [Fact]
    public async System.Threading.Tasks.Task MoveMessagesAsync_WithNullSourceFolder_DoesNotRemoveInbox() {
        var labelsJson = "{\"labels\":[{\"id\":\"Label_Target\",\"name\":\"Archive\"}]}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(labelsJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) });
        var browser = CreateBrowser(handler);

        var results = await browser.MoveMessagesAsync(new[] { "m1", "m2" }, sourceFolder: null, targetFolder: "Archive");

        Assert.Equal(2, results.Count);
        Assert.All(results, x => Assert.True(x.Ok, x.Error));
        Assert.Equal(2, handler.Requests.Count);
        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"ids\":[\"m1\",\"m2\"]", body, StringComparison.Ordinal);
        Assert.Contains("\"addLabelIds\":[\"Label_Target\"]", body, StringComparison.Ordinal);
        Assert.Contains("\"removeLabelIds\":[\"TRASH\"]", body, StringComparison.Ordinal);
        Assert.DoesNotContain("INBOX", body, StringComparison.Ordinal);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteThreadsAsync_MapsPerThreadFailures() {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.NoContent) { Content = new StringContent(string.Empty) },
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") });
        var browser = CreateBrowser(handler);

        var results = await browser.DeleteThreadsAsync(new[] { "t1", "t2" });

        Assert.Equal(2, results.Count);
        Assert.Equal("t1", results[0].Id);
        Assert.True(results[0].Ok, results[0].Error);
        Assert.Equal("t2", results[1].Id);
        Assert.False(results[1].Ok);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/users/me/threads/t1", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("/users/me/threads/t2", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task MoveThreadsAsync_UsesThreadModifyLabels() {
        var labelsJson = "{\"labels\":[{\"id\":\"Label_1\",\"name\":\"Project\"}]}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(labelsJson) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"t1\"}") });
        var browser = CreateBrowser(handler);

        var results = await browser.MoveThreadsAsync(new[] { "t1" }, sourceFolder: "INBOX", targetFolder: "Project");

        Assert.Single(results);
        Assert.True(results[0].Ok, results[0].Error);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/users/me/labels", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("/users/me/threads/t1/modify", handler.Requests[1].RequestUri!.ToString());
        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"addLabelIds\":[\"Label_1\"]", body, StringComparison.Ordinal);
        Assert.Contains("\"removeLabelIds\":[\"INBOX\",\"TRASH\"]", body, StringComparison.Ordinal);
    }

    [Fact]
    public async System.Threading.Tasks.Task SetThreadsSeenAsync_UsesThreadModifyLabels() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"t1\"}") });
        var browser = CreateBrowser(handler);

        var results = await browser.SetThreadsSeenAsync(new[] { "t1" }, seen: true);

        Assert.Single(results);
        Assert.True(results[0].Ok, results[0].Error);
        Assert.Single(handler.Requests);
        Assert.Contains("/users/me/threads/t1/modify", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"removeLabelIds\":[\"UNREAD\"]", body, StringComparison.Ordinal);
    }

    [Fact]
    public async System.Threading.Tasks.Task SetThreadsFlaggedAsync_UsesThreadModifyLabels() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"t1\"}") });
        var browser = CreateBrowser(handler);

        var results = await browser.SetThreadsFlaggedAsync(new[] { "t1" }, flagged: false);

        Assert.Single(results);
        Assert.True(results[0].Ok, results[0].Error);
        Assert.Single(handler.Requests);
        Assert.Contains("/users/me/threads/t1/modify", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"removeLabelIds\":[\"STARRED\"]", body, StringComparison.Ordinal);
    }

    private static Dictionary<string, List<string>> ParseQueryParams(Uri uri) {
        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var q = uri.Query;
        if (string.IsNullOrEmpty(q) || q == "?") {
            return dict;
        }
        if (q[0] == '?') {
            q = q.Substring(1);
        }

        foreach (var part in q.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)) {
            var idx = part.IndexOf('=');
            string rawKey;
            string rawValue;
            if (idx < 0) {
                rawKey = part;
                rawValue = string.Empty;
            } else {
                rawKey = part.Substring(0, idx);
                rawValue = part.Substring(idx + 1);
            }

            var key = Uri.UnescapeDataString(rawKey.Replace("+", " "));
            var value = Uri.UnescapeDataString(rawValue.Replace("+", " "));

            if (!dict.TryGetValue(key, out var list)) {
                list = new List<string>();
                dict[key] = list;
            }
            list.Add(value);
        }

        return dict;
    }

    private static GmailMailboxBrowser CreateBrowser(HttpMessageHandler handler) {
        var api = new GmailApiClient(new OAuthCredential { UserName = "me", AccessToken = "token", ExpiresOn = DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(api, new HttpClient(handler) { BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/") });
        return new GmailMailboxBrowser(api);
    }
}
