using System.Reflection;
using System.Net.Http;
using Xunit;

namespace Mailozaurr.Tests;

public class GmailApiClientTests {
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
