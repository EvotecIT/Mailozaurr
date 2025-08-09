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
}
