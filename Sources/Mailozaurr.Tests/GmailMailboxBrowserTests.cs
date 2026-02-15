using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
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
    public async System.Threading.Tasks.Task GetMessageContentAsync_ReturnsMimeAndFlags() {
        var mime = "From: a@example.test\r\nTo: b@example.test\r\nSubject: Sample\r\nMessage-Id: <m1@example.test>\r\n\r\nhello";
        var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(mime)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var rawJson = "{\"id\":\"m1\",\"labelIds\":[\"UNREAD\",\"STARRED\"],\"raw\":\"" + raw + "\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(rawJson) });
        var browser = CreateBrowser(handler);

        var result = await browser.GetMessageContentAsync("m1");

        Assert.NotNull(result.Message);
        Assert.Equal("Sample", result.Message.Subject);
        Assert.False(result.Seen);
        Assert.True(result.Flagged);
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
