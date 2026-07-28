using Mailozaurr;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public class MicrosoftGraphUtilsPagingTests {
    private static FieldInfo GetHandlerField() =>
        typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("HttpClient handler field not found");

    [Fact]
    public async Task GetMailMessagesAsync_FollowsNextLinkAndRespectsLimit() {
        var page1 = new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"value\":[{\"id\":\"1\"},{\"id\":\"2\"}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/users/u/messages?$skip=2\"}")
        };
        var page2 = new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"value\":[{\"id\":\"3\"},{\"id\":\"4\"}]}")
        };
        var handler = new RecordingHandler(page1, page2);
        var httpClientField = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)httpClientField.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        var tokenCacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var tokenCache = (ConcurrentDictionary<string, GraphAuthorization>)tokenCacheField.GetValue(null)!;
        var cred = new GraphCredential { ClientId = "id", DirectoryId = "tenant", ClientSecret = "secret" };
        var key = MicrosoftGraphUtils.BuildGraphTokenCacheKey(
            cred,
            "tenant",
            "https://graph.microsoft.com");
        tokenCache[key] = new GraphAuthorization { AccessToken = "token", TokenType = "Bearer", ExpiresOn = DateTimeOffset.UtcNow.AddHours(1) };
        try {
            var messages = await MicrosoftGraphUtils.GetMailMessagesAsync(cred, "u", limit: 3);
            Assert.Equal(3, messages.Count);
            Assert.Equal("1", messages[0]["id"]);
            Assert.Equal("2", messages[1]["id"]);
            Assert.Equal("3", messages[2]["id"]);
            Assert.Equal("https://graph.microsoft.com/v1.0/users/u/messages?$skip=2", handler.Requests[1].RequestUri!.AbsoluteUri);
            Assert.Equal(2, handler.Requests.Count);
        } finally {
            handlerField.SetValue(client, original);
            tokenCache.TryRemove(key, out _);
        }
    }

    [Fact]
    public async Task GetMailMessagesAsync_CanBeCancelledDuringPaging() {
        const string firstPage = "{\"value\":[{\"id\":\"1\"}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/users/u/messages?$skip=1\"}";
        var handler = new BlockingHandler(firstPage);
        var httpClientField = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)httpClientField.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        var tokenCacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var tokenCache = (ConcurrentDictionary<string, GraphAuthorization>)tokenCacheField.GetValue(null)!;
        var cred = new GraphCredential { ClientId = "id", DirectoryId = "tenant", ClientSecret = "secret" };
        var key = MicrosoftGraphUtils.BuildGraphTokenCacheKey(
            cred,
            "tenant",
            "https://graph.microsoft.com");
        tokenCache[key] = new GraphAuthorization { AccessToken = "token", TokenType = "Bearer", ExpiresOn = DateTimeOffset.UtcNow.AddHours(1) };
        using var cts = new CancellationTokenSource();
        try {
            var task = MicrosoftGraphUtils.GetMailMessagesAsync(cred, "u", cancellationToken: cts.Token);
            await handler.FirstRequestProcessed.Task;
            var secondRequestObserved = await Task.WhenAny(handler.SecondRequestStarted.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(handler.SecondRequestStarted.Task, secondRequestObserved);
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
            Assert.Equal(2, handler.Requests.Count);
        } finally {
            handlerField.SetValue(client, original);
            tokenCache.TryRemove(key, out _);
        }
    }

    [Fact]
    public async Task GetMailMessageAttachmentsAsync_FollowsNextLink() {
        var page1 = new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"value\":[{\"name\":\"a1\",\"contentBytes\":\"QQ==\"}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/users/u/messages/m/attachments?$skip=1\"}")
        };
        var page2 = new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"value\":[{\"name\":\"a2\",\"contentBytes\":\"Qg==\"}]}")
        };
        var handler = new RecordingHandler(page1, page2);
        var httpClientField = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)httpClientField.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        var tokenCacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var tokenCache = (ConcurrentDictionary<string, GraphAuthorization>)tokenCacheField.GetValue(null)!;
        var cred = new GraphCredential { ClientId = "id", DirectoryId = "tenant", ClientSecret = "secret" };
        var key = MicrosoftGraphUtils.BuildGraphTokenCacheKey(
            cred,
            "tenant",
            "https://graph.microsoft.com");
        tokenCache[key] = new GraphAuthorization { AccessToken = "token", TokenType = "Bearer", ExpiresOn = DateTimeOffset.UtcNow.AddHours(1) };
        try {
            var attachments = await MicrosoftGraphUtils.GetMailMessageAttachmentsAsync(cred, "u", "m");

            Assert.Equal(2, attachments.Count);
            Assert.Equal("a1", attachments[0].Name);
            Assert.Equal("a2", attachments[1].Name);
            Assert.Equal("https://graph.microsoft.com/v1.0/users/u/messages/m/attachments?$skip=1", handler.Requests[1].RequestUri!.AbsoluteUri);
            Assert.Equal(2, handler.Requests.Count);
        } finally {
            handlerField.SetValue(client, original);
            tokenCache.TryRemove(key, out _);
        }
    }

    [Fact]
    public async Task GetMailFoldersAsync_FollowsNextLink() {
        var page1 = new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"value\":[{\"id\":\"f1\"}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/users/u/mailFolders?$skip=1\"}")
        };
        var page2 = new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"value\":[{\"id\":\"f2\"}]}")
        };
        var handler = new RecordingHandler(page1, page2);
        var httpClientField = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)httpClientField.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        var tokenCacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var tokenCache = (ConcurrentDictionary<string, GraphAuthorization>)tokenCacheField.GetValue(null)!;
        var cred = new GraphCredential { ClientId = "id", DirectoryId = "tenant", ClientSecret = "secret" };
        var key = MicrosoftGraphUtils.BuildGraphTokenCacheKey(
            cred,
            "tenant",
            "https://graph.microsoft.com");
        tokenCache[key] = new GraphAuthorization { AccessToken = "token", TokenType = "Bearer", ExpiresOn = DateTimeOffset.UtcNow.AddHours(1) };
        try {
            var folders = await MicrosoftGraphUtils.GetMailFoldersAsync(cred, "u");

            Assert.Equal(2, folders.Count);
            Assert.Equal("f1", folders[0].GetProperty("id").GetString());
            Assert.Equal("f2", folders[1].GetProperty("id").GetString());
            Assert.Equal("https://graph.microsoft.com/v1.0/users/u/mailFolders?$skip=1", handler.Requests[1].RequestUri!.AbsoluteUri);
            Assert.Equal(2, handler.Requests.Count);
        } finally {
            handlerField.SetValue(client, original);
            tokenCache.TryRemove(key, out _);
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler {
        private readonly string _firstPageJson;
        public List<HttpRequestMessage> Requests { get; } = new();
        public TaskCompletionSource<bool> FirstRequestProcessed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SecondRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingHandler(string firstPageJson) {
            _firstPageJson = firstPageJson;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var copy = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers) {
                copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            if (request.Content != null) {
                var bytes = await request.Content.ReadAsByteArrayAsync();
                copy.Content = new ByteArrayContent(bytes);
                foreach (var header in request.Content.Headers) {
                    copy.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
            Requests.Add(copy);
            if (Requests.Count == 1) {
                FirstRequestProcessed.TrySetResult(true);
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(_firstPageJson)
                };
            }

            SecondRequestStarted.TrySetResult(true);
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{\"value\":[]}")
            };
        }
    }
}
