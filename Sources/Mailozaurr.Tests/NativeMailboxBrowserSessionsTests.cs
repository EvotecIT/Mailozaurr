using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class NativeMailboxBrowserSessionsTests {
    [Fact]
    public async Task GraphMailboxBrowserSession_UsesProvidedHttpClientAndCredential() {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"value\":[]}") });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var credential = new OAuthCredential { UserName = "me", AccessToken = "graph-token", ExpiresOn = DateTimeOffset.MaxValue };

        using var session = new GraphMailboxBrowserSession(client, credential);
        var folders = await session.Browser.ListFoldersAsync();

        Assert.Empty(folders);
        Assert.Single(handler.Requests);
        Assert.Equal("https://graph.microsoft.com/v1.0/me/mailFolders", handler.Requests[0].RequestUri!.GetLeftPart(UriPartial.Path));
        Assert.Contains("$top=200", handler.Requests[0].RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("displayName", handler.Requests[0].RequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal("graph-token", handler.Requests[0].Headers.Authorization?.Parameter);
    }

    [Fact]
    public void GraphMailboxBrowserSession_CreateWithAccessToken_RejectsEmptyToken() {
        var ex = Assert.Throws<ArgumentException>(() => GraphMailboxBrowserSession.CreateWithAccessToken(" "));
        Assert.Equal("accessToken", ex.ParamName);
    }

    [Fact]
    public async Task GmailMailboxBrowserSession_UsesProvidedUserIdAndCredential() {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"labels\":[]}") });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/") };
        var credential = new OAuthCredential { UserName = "mailbox-user", AccessToken = "gmail-token", ExpiresOn = DateTimeOffset.MaxValue };

        using var session = new GmailMailboxBrowserSession(client, credential, userId: "custom-user");
        var labels = await session.Browser.ListFoldersAsync();

        Assert.Empty(labels);
        Assert.Single(handler.Requests);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/custom-user/labels?fields=labels(id,name,type,messageListVisibility,labelListVisibility,messagesTotal,messagesUnread,threadsTotal,threadsUnread,color)", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal("gmail-token", handler.Requests[0].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GmailMailboxBrowserSession_CreateWithAccessToken_DefaultsToMeUserId() {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"labels\":[]}") });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/") };

        using var session = GmailMailboxBrowserSession.CreateWithAccessToken("token-1", client: client);
        var labels = await session.Browser.ListFoldersAsync();

        Assert.Empty(labels);
        Assert.Single(handler.Requests);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/labels?fields=labels(id,name,type,messageListVisibility,labelListVisibility,messagesTotal,messagesUnread,threadsTotal,threadsUnread,color)", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GraphMailboxBrowserSession_Dispose_IsIdempotent_AndPreventsFurtherUse() {
        var handler = new RecordingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var credential = new OAuthCredential { UserName = "me", AccessToken = "graph-token", ExpiresOn = DateTimeOffset.MaxValue };
        var session = new GraphMailboxBrowserSession(client, credential);

        session.Dispose();
        session.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.Browser.ListFoldersAsync());
    }

    [Fact]
    public async Task GraphMailboxBrowserSession_Dispose_DoesNotDisposeProvidedHttpClient() {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"value\":[]}") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var credential = new OAuthCredential { UserName = "me", AccessToken = "graph-token", ExpiresOn = DateTimeOffset.MaxValue };
        var session = new GraphMailboxBrowserSession(client, credential);

        await session.Browser.ListFoldersAsync();
        session.Dispose();

        using var response = await client.GetAsync("me");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GmailMailboxBrowserSession_Dispose_IsIdempotent_AndPreventsFurtherUse() {
        var handler = new RecordingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/") };
        var credential = new OAuthCredential { UserName = "me", AccessToken = "gmail-token", ExpiresOn = DateTimeOffset.MaxValue };
        var session = new GmailMailboxBrowserSession(client, credential);

        session.Dispose();
        session.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.Browser.ListFoldersAsync());
    }

    [Fact]
    public async Task GmailMailboxBrowserSession_Dispose_DoesNotDisposeProvidedHttpClient() {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"labels\":[]}") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/") };
        var credential = new OAuthCredential { UserName = "me", AccessToken = "gmail-token", ExpiresOn = DateTimeOffset.MaxValue };
        var session = new GmailMailboxBrowserSession(client, credential);

        await session.Browser.ListFoldersAsync();
        session.Dispose();

        using var response = await client.GetAsync("users/me/profile");
        Assert.True(response.IsSuccessStatusCode);
    }
}
