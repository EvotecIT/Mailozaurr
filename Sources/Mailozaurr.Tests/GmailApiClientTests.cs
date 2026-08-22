using System.Net.Http;
using System.Reflection;
using Xunit;

namespace Mailozaurr.Tests;

public class GmailApiClientTests {
    private static Dictionary<string, List<string>> ParseQueryParams(Uri uri) {
        // Uri.ToString()/Query behave differently across runtimes (notably net472 vs net8).
        // Parse and compare decoded query params so tests are stable across TFMs.
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

            // Query encoding may use '+' for space; normalize before unescaping.
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

    private sealed class CancelAwareHandler : HttpMessageHandler {
        private readonly HttpResponseMessage _response;
        public CancelAwareHandler(HttpResponseMessage response) => _response = response;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_response);
        }
    }

    private sealed class DisposingHandler : HttpMessageHandler {
        public bool Disposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));

        protected override void Dispose(bool disposing) {
            if (disposing) {
                Disposed = true;
            }
            base.Dispose(disposing);
        }
    }

    private sealed class PendingRepositoryStub : IPendingMessageRepository {
        public List<PendingMessageRecord> Saved { get; } = new();

        public Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) {
            Saved.Add(record);
            return Task.CompletedTask;
        }

        public Task<PendingMessageRecord?> TryAcquireLeaseAsync(
            string messageId,
            DateTimeOffset dueBeforeOrAt,
            DateTimeOffset leaseUntil,
            CancellationToken cancellationToken = default) {
            var record = Saved.FirstOrDefault(r => string.Equals(r.MessageId, messageId, StringComparison.OrdinalIgnoreCase));
            if (record == null || record.NextAttemptAt > dueBeforeOrAt) {
                return Task.FromResult<PendingMessageRecord?>(null);
            }

            record.NextAttemptAt = leaseUntil;
            return Task.FromResult<PendingMessageRecord?>(record);
        }

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PendingMessageRecord?>(Saved.FirstOrDefault(r => string.Equals(r.MessageId, messageId, StringComparison.OrdinalIgnoreCase)));

        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
            foreach (var record in Saved) {
                cancellationToken.ThrowIfCancellationRequested();
                yield return record;
                await Task.Yield();
            }
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
            Saved.RemoveAll(r => string.Equals(r.MessageId, messageId, StringComparison.OrdinalIgnoreCase));
            return Task.CompletedTask;
        }
    }

    private sealed class DelayedCancellationHandler : HttpMessageHandler {
        private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitForStartAsync() => _started.Task;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            _started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
    }
    [Fact]
    public void Constructor_SetsAuthorizationHeader() {
        var cred = new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue };
        var client = new GmailApiClient(cred);
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var httpClient = (System.Net.Http.HttpClient)field.GetValue(client)!;
        Assert.Equal("Bearer", httpClient.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal("t", httpClient.DefaultRequestHeaders.Authorization?.Parameter);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteAsync_SendsDeleteRequest() {
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        await client.DeleteAsync("me", "123");
        Assert.Single(handler.Requests);
        Assert.Equal(System.Net.Http.HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/messages/123", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task GetProfileAsync_ReturnsProfile() {
        var json = "{\"emailAddress\":\"a@b.com\",\"messagesTotal\":1,\"threadsTotal\":2,\"historyId\":\"10\"}";
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        var profile = await client.GetProfileAsync("me");
        Assert.Equal("a@b.com", profile.EmailAddress);
        Assert.Equal(1, profile.MessagesTotal);
        Assert.Equal(2, profile.ThreadsTotal);
        Assert.Equal("10", profile.HistoryId);
        Assert.Single(handler.Requests);
        Assert.Equal(System.Net.Http.HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/profile", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task GetProfileWithoutRefreshAsync_PreservesForbiddenEvidenceWithoutRefreshing() {
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Forbidden) {
            Content = new System.Net.Http.StringContent("{\"error\":{\"code\":403}}")
        });
        var refreshes = 0;
        var httpClient = new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") };
        var client = new GmailApiClient(httpClient, _ => {
            refreshes++;
            return System.Threading.Tasks.Task.FromResult("new-token");
        });

        var exception = await Assert.ThrowsAsync<GmailAuthenticationException>(() =>
            client.GetProfileWithoutRefreshAsync("me"));

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal(0, refreshes);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async System.Threading.Tasks.Task WatchAsync_SendsRequestAndParsesResponse() {
        var json = "{\"historyId\":\"1\",\"expiration\":\"12345\"}";
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        var result = await client.WatchAsync("me", "projects/p/topics/t", new[] { "INBOX", "SENT" });
        Assert.Equal("1", result.HistoryId);
        Assert.Equal(12345, result.Expiration);

        Assert.Single(handler.Requests);
        Assert.Equal(System.Net.Http.HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/watch", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"topicName\":\"projects/p/topics/t\"", body);
        Assert.Contains("\"labelIds\":[\"INBOX\",\"SENT\"]", body);
        Assert.Contains("\"labelFilterAction\":\"include\"", body);
    }

    [Fact]
    public async System.Threading.Tasks.Task StopWatchAsync_SendsStopRequest() {
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("{}") });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        await client.StopWatchAsync("me");
        Assert.Single(handler.Requests);
        Assert.Equal(System.Net.Http.HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/stop", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Equal("{}", body);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListHistoryAsync_BuildsQueryAndParsesResponse() {
        var json = "{\"history\":[{\"id\":\"1\",\"messagesAdded\":[{\"message\":{\"id\":\"m1\",\"threadId\":\"t1\"}}]}],\"historyId\":\"10\",\"nextPageToken\":\"pt\"}";
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        var result = await client.ListHistoryAsync("me", "5", labelId: "INBOX", historyTypes: new[] { "messageAdded", "labelAdded" }, maxResults: 3, pageToken: "tok");
        Assert.Equal("10", result.HistoryId);
        Assert.Equal("pt", result.NextPageToken);
        Assert.Single(result.History!);
        Assert.Equal("1", result.History![0].Id);
        Assert.Equal("m1", result.History![0].MessagesAdded![0].Message!.Id);
        Assert.Equal("t1", result.History![0].MessagesAdded![0].Message!.ThreadId);

        Assert.Single(handler.Requests);
        var uri = handler.Requests[0].RequestUri!.ToString();
        Assert.Contains("https://gmail.googleapis.com/gmail/v1/users/me/history?", uri);
        Assert.Contains("startHistoryId=5", uri);
        Assert.Contains("labelId=INBOX", uri);
        Assert.Contains("maxResults=3", uri);
        Assert.Contains("pageToken=tok", uri);
        Assert.Contains("historyTypes=messageAdded", uri);
        Assert.Contains("historyTypes=labelAdded", uri);
    }

    [Fact]
    public async System.Threading.Tasks.Task DownloadAttachmentAsync_ReturnsBytes() {
        var json = "{\"data\":\"dGVzdA\"}"; // "test" base64url
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        var data = await client.DownloadAttachmentAsync("me", "123", "att");
        Assert.Equal(4, data.Length);
        Assert.Equal(new byte[] { 116, 101, 115, 116 }, data);
        Assert.Single(handler.Requests);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/messages/123/attachments/att", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task DownloadAttachmentAsync_NoData_ReturnsEmptyArray() {
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("{}") });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        var data = await client.DownloadAttachmentAsync("me", "123", "att");
        Assert.Empty(data);
    }

    [Fact]
    public async System.Threading.Tasks.Task DownloadAttachmentAsync_InvalidData_Throws() {
        var json = "{\"data\":\"abcde\"}"; // invalid length base64url
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        await Assert.ThrowsAsync<System.IO.InvalidDataException>(() => client.DownloadAttachmentAsync("me", "123", "att"));
    }

    [Fact]
    public async System.Threading.Tasks.Task ListLabelsAsync_SendsRequestAndParsesResponse() {
        var json = "{\"labels\":[{\"id\":\"INBOX\",\"name\":\"Inbox\",\"type\":\"system\"},{\"id\":\"Label_1\",\"name\":\"X\",\"type\":\"user\"}]}";
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        var labels = await client.ListLabelsAsync("me");
        Assert.Equal(2, labels.Count);
        Assert.Equal("INBOX", labels[0].Id);
        Assert.Equal("Inbox", labels[0].Name);
        Assert.Equal("system", labels[0].Type);

        Assert.Single(handler.Requests);
        Assert.Equal(System.Net.Http.HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/labels?fields=labels(id,name,type)", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task ListPageAsync_BuildsQueryAndParsesResponse() {
        var json = "{\"messages\":[{\"id\":\"m1\",\"threadId\":\"t1\"},{\"id\":\"m2\",\"threadId\":\"t2\"}],\"nextPageToken\":\"pt\",\"resultSizeEstimate\":\"123\"}";
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        var page = await client.ListPageAsync(
            "me",
            query: "from:test@example.com",
            labelIds: new[] { "INBOX", "Label_1" },
            includeSpamTrash: true,
            maxResults: 999,
            pageToken: "tok",
            fields: "messages(id,threadId),nextPageToken,resultSizeEstimate");

        Assert.NotNull(page.Messages);
        Assert.Equal(2, page.Messages!.Count);
        Assert.Equal("m1", page.Messages[0].Id);
        Assert.Equal("t1", page.Messages[0].ThreadId);
        Assert.Equal("pt", page.NextPageToken);
        Assert.Equal(123, page.ResultSizeEstimate);

        Assert.Single(handler.Requests);
        var uri = handler.Requests[0].RequestUri!;
        Assert.StartsWith("https://gmail.googleapis.com/gmail/v1/users/me/messages", uri.ToString());

        var qp = ParseQueryParams(uri);
        Assert.Equal("from:test@example.com", Assert.Single(qp["q"]));
        Assert.Equal("500", Assert.Single(qp["maxResults"])); // clamped
        Assert.Equal("tok", Assert.Single(qp["pageToken"]));
        Assert.Equal("true", Assert.Single(qp["includeSpamTrash"]));
        Assert.Contains("INBOX", qp["labelIds"]);
        Assert.Contains("Label_1", qp["labelIds"]);
        Assert.Equal("messages(id,threadId),nextPageToken,resultSizeEstimate", Assert.Single(qp["fields"]));
    }

    [Fact]
    public async System.Threading.Tasks.Task GetRawAsync_BuildsQueryAndParsesResponse() {
        var json = "{\"id\":\"m1\",\"threadId\":\"t1\",\"internalDate\":\"1700000\",\"labelIds\":[\"UNREAD\"],\"raw\":\"dGVzdA\"}";
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        var msg = await client.GetRawAsync("me", "m1", fields: "id,threadId,internalDate,labelIds,raw");
        Assert.Equal("m1", msg.Id);
        Assert.Equal("t1", msg.ThreadId);
        Assert.Equal(1700000, msg.InternalDate);
        Assert.Single(msg.LabelIds!);
        Assert.Equal("UNREAD", msg.LabelIds![0]);
        Assert.Equal("dGVzdA", msg.Raw);

        Assert.Single(handler.Requests);
        var uri = handler.Requests[0].RequestUri!.ToString();
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/messages/m1?format=raw&fields=id%2CthreadId%2CinternalDate%2ClabelIds%2Craw", uri);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetThreadAsync_WithFormatAndFields_BuildsQuery() {
        var json = "{\"id\":\"t1\",\"messages\":[{\"id\":\"m1\",\"threadId\":\"t1\"}]}";
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        var thread = await client.GetThreadWithOptionsAsync("me", "t1", format: "full", fields: "id,messages(id,threadId)");
        Assert.Equal("t1", thread.Id);
        Assert.NotNull(thread.Messages);
        Assert.Single(thread.Messages!);
        Assert.Equal("m1", thread.Messages![0].Id);

        Assert.Single(handler.Requests);
        var uri = handler.Requests[0].RequestUri!;
        Assert.StartsWith("https://gmail.googleapis.com/gmail/v1/users/me/threads/t1", uri.ToString());
        var qp = ParseQueryParams(uri);
        Assert.Equal("full", Assert.Single(qp["format"]));
        Assert.Equal("id,messages(id,threadId)", Assert.Single(qp["fields"]));
    }

    [Fact]
    public async System.Threading.Tasks.Task GetMessageWithOptionsAsync_MetadataFormat_BuildsQuery() {
        var json = "{\"id\":\"m1\",\"threadId\":\"t1\",\"payload\":{\"headers\":[{\"name\":\"Message-ID\",\"value\":\"<x>\"}]}}";
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        var msg = await client.GetMessageWithOptionsAsync(
            "me",
            "m1",
            format: "metadata",
            metadataHeaders: new[] { "Message-ID", "References" },
            fields: "id,payload(headers)");

        Assert.Equal("m1", msg.Id);
        Assert.NotNull(msg.Payload);
        Assert.NotNull(msg.Payload!.Headers);
        Assert.Single(msg.Payload!.Headers!);
        Assert.Equal("Message-ID", msg.Payload!.Headers![0].Name);

        Assert.Single(handler.Requests);
        var uri = handler.Requests[0].RequestUri!;
        Assert.StartsWith("https://gmail.googleapis.com/gmail/v1/users/me/messages/m1", uri.ToString());
        var qp = ParseQueryParams(uri);
        Assert.Equal("metadata", Assert.Single(qp["format"]));
        Assert.Equal("id,payload(headers)", Assert.Single(qp["fields"]));
        Assert.Contains("Message-ID", qp["metadataHeaders"]);
        Assert.Contains("References", qp["metadataHeaders"]);
    }

    [Fact]
    public async System.Threading.Tasks.Task ImportAsync_BuildsQueryAndSendsBody() {
        var json = "{\"id\":\"m1\",\"threadId\":\"t1\"}";
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        var msg = await client.ImportAsync("me", raw: "dGVzdA", labelIds: new[] { "SENT" });
        Assert.Equal("m1", msg.Id);

        Assert.Single(handler.Requests);
        Assert.Equal(System.Net.Http.HttpMethod.Post, handler.Requests[0].Method);
        var uri = handler.Requests[0].RequestUri!;
        Assert.StartsWith("https://gmail.googleapis.com/gmail/v1/users/me/messages/import", uri.ToString());
        var qp = ParseQueryParams(uri);
        Assert.Equal("dateHeader", Assert.Single(qp["internalDateSource"]));
        Assert.Equal("true", Assert.Single(qp["neverMarkSpam"]));

        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"raw\":\"dGVzdA\"", body);
        Assert.Contains("\"labelIds\":[\"SENT\"]", body);
    }

    [Fact]
    public async System.Threading.Tasks.Task ModifyMessageLabelsAsync_SendsModifyRequest() {
        var json = "{\"id\":\"m1\",\"threadId\":\"t1\"}";
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        var message = await client.ModifyMessageLabelsAsync("me", "m1", addLabelIds: new[] { "INBOX" }, removeLabelIds: new[] { "UNREAD" });
        Assert.Equal("m1", message.Id);
        Assert.Equal("t1", message.ThreadId);

        Assert.Single(handler.Requests);
        Assert.Equal(System.Net.Http.HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/messages/m1/modify", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"addLabelIds\":[\"INBOX\"]", body);
        Assert.Contains("\"removeLabelIds\":[\"UNREAD\"]", body);
    }

    [Fact]
    public async System.Threading.Tasks.Task TrashMessageAsync_SendsTrashRequest() {
        var json = "{\"id\":\"m1\",\"threadId\":\"t1\"}";
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        var message = await client.TrashMessageAsync("me", "m1");
        Assert.Equal("m1", message.Id);

        Assert.Single(handler.Requests);
        Assert.Equal(System.Net.Http.HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/messages/m1/trash", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task BatchModifyMessagesAsync_SendsBatchModifyRequest() {
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("{}") });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        await client.BatchModifyMessagesAsync("me", new[] { "m1", "m2" }, addLabelIds: new[] { "INBOX" }, removeLabelIds: new[] { "TRASH" });

        Assert.Single(handler.Requests);
        Assert.Equal(System.Net.Http.HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/messages/batchModify", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"ids\":[\"m1\",\"m2\"]", body);
        Assert.Contains("\"addLabelIds\":[\"INBOX\"]", body);
        Assert.Contains("\"removeLabelIds\":[\"TRASH\"]", body);
    }

    [Fact]
    public async System.Threading.Tasks.Task BatchDeleteMessagesAsync_SendsBatchDeleteRequest() {
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("{}") });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        await client.BatchDeleteMessagesAsync("me", new[] { "m1", "m2" });

        Assert.Single(handler.Requests);
        Assert.Equal(System.Net.Http.HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/messages/batchDelete", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"ids\":[\"m1\",\"m2\"]", body);
    }

    [Fact]
    public async System.Threading.Tasks.Task ModifyThreadLabelsAsync_SendsModifyRequest() {
        var json = "{\"id\":\"t1\",\"messages\":[{\"id\":\"m1\"}]}";
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        var thread = await client.ModifyThreadLabelsAsync("me", "t1", addLabelIds: new[] { "INBOX" }, removeLabelIds: new[] { "TRASH" });
        Assert.Equal("t1", thread.Id);
        Assert.Single(thread.Messages!);

        Assert.Single(handler.Requests);
        Assert.Equal(System.Net.Http.HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/threads/t1/modify", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"addLabelIds\":[\"INBOX\"]", body);
        Assert.Contains("\"removeLabelIds\":[\"TRASH\"]", body);
    }

    [Fact]
    public async System.Threading.Tasks.Task TrashThreadAsync_SendsTrashRequest() {
        var json = "{\"id\":\"t1\",\"messages\":[]}";
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        var thread = await client.TrashThreadAsync("me", "t1");
        Assert.Equal("t1", thread.Id);

        Assert.Single(handler.Requests);
        Assert.Equal(System.Net.Http.HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/threads/t1/trash", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteThreadAsync_SendsDeleteRequest() {
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        await client.DeleteThreadAsync("me", "t1");

        Assert.Single(handler.Requests);
        Assert.Equal(System.Net.Http.HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/threads/t1", handler.Requests[0].RequestUri!.ToString());
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.Unauthorized)]
    [InlineData(System.Net.HttpStatusCode.Forbidden)]
    public async System.Threading.Tasks.Task SendAsync_AuthError_ThrowsGmailAuthenticationException(System.Net.HttpStatusCode status) {
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(status) { Content = new System.Net.Http.StringContent("error") });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        var message = new MimeKit.MimeMessage();
        await Assert.ThrowsAsync<GmailAuthenticationException>(() => client.SendAsync("me", message));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async System.Threading.Tasks.Task SendAsync_ExpiredToken_InvokesRefresh() {
        var handler = new RecordingHandler(
            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized) { Content = new System.Net.Http.StringContent("error") },
            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("{\"id\":\"1\"}") });
        int refreshes = 0;
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task<string>> refresher = _ => {
            refreshes++;
            return System.Threading.Tasks.Task.FromResult("new");
        };
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "old", ExpiresOn = System.DateTimeOffset.MaxValue }, refresher);
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var httpClient = new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") };
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "old");
        field.SetValue(client, httpClient);
        var message = new MimeKit.MimeMessage();
        await Assert.ThrowsAsync<GmailAuthenticationException>(() => client.SendAsync("me", message));
        Assert.Equal(1, refreshes);
        await client.SendAsync("me", message);
        Assert.Equal("Bearer old", handler.Requests[0].Headers.Authorization!.ToString());
        Assert.Equal("Bearer new", handler.Requests[1].Headers.Authorization!.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task ListAsync_PaginatesUntilTokenNull() {
        var page1 = "{\"messages\":[{\"id\":\"1\"}],\"nextPageToken\":\"tok\"}";
        var page2 = "{\"messages\":[{\"id\":\"2\"}]}";
        var handler = new RecordingHandler(
            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(page1) },
            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(page2) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        var list = await client.ListAsync("me");
        Assert.Equal(2, list.Count);
        Assert.Equal("1", list[0].Id);
        Assert.Equal("2", list[1].Id);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(string.Empty, handler.Requests[0].RequestUri!.Query);
        Assert.Equal("?pageToken=tok", handler.Requests[1].RequestUri!.Query);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListAsync_ExactMaxResults_StopsEarly() {
        var page1 = "{\"messages\":[{\"id\":\"1\"},{\"id\":\"2\"}],\"nextPageToken\":\"tok\"}";
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(page1) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        var list = await client.ListAsync("me", maxResults: 2);
        Assert.Equal(2, list.Count);
        Assert.Single(handler.Requests);
        Assert.Equal("?maxResults=2", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListAsync_PartialPageLimit_AdjustsRequests() {
        var page1 = "{\"messages\":[{\"id\":\"1\"},{\"id\":\"2\"}],\"nextPageToken\":\"tok\"}";
        var page2 = "{\"messages\":[{\"id\":\"3\"}],\"nextPageToken\":\"tok2\"}";
        var handler = new RecordingHandler(
            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(page1) },
            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(page2) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        var list = await client.ListAsync("me", maxResults: 3);
        Assert.Equal(3, list.Count);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("?maxResults=3", handler.Requests[0].RequestUri!.Query);
        Assert.Equal("?maxResults=1&pageToken=tok", handler.Requests[1].RequestUri!.Query);
    }

    [Fact]
    public async System.Threading.Tasks.Task SendAsync_CanBeCancelled() {
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();
        var message = new MimeKit.MimeMessage();
        await Assert.ThrowsAnyAsync<System.OperationCanceledException>(() => client.SendAsync("me", message, cts.Token));
    }

    [Fact]
    public async System.Threading.Tasks.Task SendAsync_CallerCancellation_DoesNotQueuePendingMessage() {
        var handler = new DelayedCancellationHandler();
        var repository = new PendingRepositoryStub();
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue }) {
            PendingMessageRepository = repository
        };
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });

        var message = new MimeKit.MimeMessage();
        message.From.Add(MimeKit.MailboxAddress.Parse("sender@example.com"));
        message.To.Add(MimeKit.MailboxAddress.Parse("recipient@example.com"));
        message.Body = new MimeKit.TextPart("plain") { Text = "body" };

        using var cts = new System.Threading.CancellationTokenSource();
        var task = client.SendAsync("me", message, cts.Token);
        await handler.WaitForStartAsync();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<System.OperationCanceledException>(async () => await task);
        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListAsync_CanBeCancelled() {
        var handler = new CancelAwareHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("{}") });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<System.OperationCanceledException>(() => client.ListAsync("me", cancellationToken: cts.Token));
    }

    [Fact]
    public async System.Threading.Tasks.Task GetAsync_CanBeCancelled() {
        var handler = new CancelAwareHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("{}") });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<System.OperationCanceledException>(() => client.GetAsync("me", "id", cts.Token));
    }

    [Fact]
    public async System.Threading.Tasks.Task DownloadAttachmentAsync_CanBeCancelled() {
        var handler = new CancelAwareHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("{\"data\":\"dGVzdA\"}") });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<System.OperationCanceledException>(() => client.DownloadAttachmentAsync("me", "m", "a", cts.Token));
    }

    [Fact]
    public async System.Threading.Tasks.Task SendAsync_NullResponse_Throws() {
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("null") });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        var message = new MimeKit.MimeMessage();
        await Assert.ThrowsAsync<System.IO.InvalidDataException>(() => client.SendAsync("me", message));
    }

    [Fact]
    public async System.Threading.Tasks.Task GetAsync_NullResponse_Throws() {
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("null") });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        await Assert.ThrowsAsync<System.IO.InvalidDataException>(() => client.GetAsync("me", "id"));
    }

    [Fact]
    public async System.Threading.Tasks.Task SendAsync_InvalidJson_ThrowsGmailApiException() {
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("not json") });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        var message = new MimeKit.MimeMessage();
        var ex = await Assert.ThrowsAsync<GmailApiException>(() => client.SendAsync("me", message));
        Assert.Equal("not json", ex.ResponseContent);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetAsync_InvalidJson_ThrowsGmailApiException() {
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("not json") });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        var ex = await Assert.ThrowsAsync<GmailApiException>(() => client.GetAsync("me", "id"));
        Assert.Equal("not json", ex.ResponseContent);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListThreadsAsync_PaginatesUntilTokenNull() {
        var page1 = "{\"threads\":[{\"id\":\"1\"}],\"nextPageToken\":\"tok\"}";
        var page2 = "{\"threads\":[{\"id\":\"2\"}]}";
        var handler = new RecordingHandler(
            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(page1) },
            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(page2) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        var list = await client.ListThreadsAsync("me");
        Assert.Equal(2, list.Count);
        Assert.Equal("1", list[0].Id);
        Assert.Equal("2", list[1].Id);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(string.Empty, handler.Requests[0].RequestUri!.Query);
        Assert.Equal("?pageToken=tok", handler.Requests[1].RequestUri!.Query);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetThreadAsync_ReturnsThread() {
        var json = "{\"id\":\"t1\",\"messages\":[{\"id\":\"m1\"}]}";
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(json) });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        var thread = await client.GetThreadAsync("me", "t1");
        Assert.Equal("t1", thread.Id);
        Assert.Single(thread.Messages!);
        Assert.Equal("m1", thread.Messages![0].Id);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListThreadsAsync_CanBeCancelled() {
        var handler = new CancelAwareHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("{}") });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<System.OperationCanceledException>(() => client.ListThreadsAsync("me", cancellationToken: cts.Token));
    }

    [Fact]
    public async System.Threading.Tasks.Task GetThreadAsync_CanBeCancelled() {
        var handler = new CancelAwareHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("{}") });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<System.OperationCanceledException>(() => client.GetThreadAsync("me", "id", cts.Token));
    }

    [Fact]
    public async System.Threading.Tasks.Task GetThreadAsync_NullResponse_Throws() {
        var handler = new RecordingHandler(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("null") });
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") });
        await Assert.ThrowsAsync<System.IO.InvalidDataException>(() => client.GetThreadAsync("me", "id"));
    }

    [Fact]
    public void Dispose_DisposesHttpClient() {
        var cred = new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue };
        var client = new GmailApiClient(cred);
        var handler = new DisposingHandler();
        var httpClient = new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://gmail.googleapis.com/gmail/v1/") };
        var field = typeof(GmailApiClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(client, httpClient);
        client.Dispose();
        Assert.True(handler.Disposed);
    }

    public static IEnumerable<object[]> DisposedMethods() {
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.SendAsync("u", new MimeKit.MimeMessage())) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.ListAsync("u")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.GetAsync("u", "id")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.GetMimeMessageAsync("u", "id")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.DeleteAsync("u", "id")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.TrashMessageAsync("u", "id")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.ModifyMessageLabelsAsync("u", "id")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.BatchModifyMessagesAsync("u", new[] { "id" })) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.BatchDeleteMessagesAsync("u", new[] { "id" })) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.ListLabelsAsync("u")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.ModifyThreadLabelsAsync("u", "id")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.TrashThreadAsync("u", "id")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.DeleteThreadAsync("u", "id")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.GetProfileAsync("u")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.GetProfileWithoutRefreshAsync("u")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.WatchAsync("u", "topic")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.StopWatchAsync("u")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.ListHistoryAsync("u", "1")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.ListThreadsAsync("u")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.GetThreadAsync("u", "id")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.ListAttachmentsAsync("u", "id")) };
        yield return new object[] { (Func<GmailApiClient, Task>)(c => c.DownloadAttachmentAsync("u", "mid", "aid")) };
    }

    [Theory]
    [MemberData(nameof(DisposedMethods))]
    public async Task Methods_AfterDispose_ThrowObjectDisposedException(Func<GmailApiClient, Task> action) {
        var client = new GmailApiClient(new OAuthCredential { UserName = "u", AccessToken = "t", ExpiresOn = System.DateTimeOffset.MaxValue });
        client.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => action(client));
    }
}
