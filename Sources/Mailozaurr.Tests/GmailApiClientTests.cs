using System.Reflection;
using Xunit;

namespace Mailozaurr.Tests;

public class GmailApiClientTests {
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
}
