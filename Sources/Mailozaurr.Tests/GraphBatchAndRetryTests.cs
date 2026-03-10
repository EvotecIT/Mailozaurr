using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public class GraphBatchAndRetryTests {
    private class BatchHandler : HttpMessageHandler {
        public HttpRequestMessage? BatchRequest;
        public string? BatchPayload;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (request.RequestUri!.AbsoluteUri.Contains("oauth2")) {
                var json = "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}";
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(json)
                };
            }
            if (request.RequestUri!.AbsoluteUri.Contains("$batch")) {
                BatchRequest = request;
                if (request.Content is not null) {
                    BatchPayload = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                var json = "{\"responses\":[{\"id\":\"1\",\"status\":202}]}";
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(json)
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private static FieldInfo GetHandlerField()
        => typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("HttpClient handler field not found");

    [Fact]
    public async Task SendMessageBatchAsync_BuildsBatchPayload() {
        var handler = new BatchHandler();
        var field = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)field.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try {
            using var graph = new Graph {
                From = "sender@example.com",
                To = new object[] { "recipient@example.com" },
                Subject = "sub",
                HTML = "body",
                ContentType = "HTML"
            };
            graph.Authenticate(new System.Net.NetworkCredential("id@tenant", "secret"));
            var result = await graph.SendMessageBatchAsync();
            Assert.True(result.Status);

            string payload = handler.BatchPayload!;
            using var doc = JsonDocument.Parse(payload);
            var req = doc.RootElement.GetProperty("requests")[0];
            Assert.Equal("1", req.GetProperty("id").GetString());
            Assert.Equal("POST", req.GetProperty("method").GetString());
            var msg = graph.MessageContainer?.Message;
            Assert.NotNull(msg);
            Assert.Equal($"users/{msg!.From!.Email!.Address!}/sendMail", req.GetProperty("url").GetString());
            Assert.Equal("application/json", req.GetProperty("headers").GetProperty("Content-Type").GetString());
            Assert.Equal("sub", req.GetProperty("body").GetProperty("message").GetProperty("subject").GetString());
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public async Task SendMessageBatchAsync_Canceled_ThrowsOperationCanceledException() {
        var handler = new BatchHandler();
        var field = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)field.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try {
            using var graph = new Graph {
                From = "sender@example.com",
                To = new object[] { "recipient@example.com" },
                Subject = "sub",
                HTML = "body",
                ContentType = "HTML"
            };
            graph.Authenticate(new System.Net.NetworkCredential("id@tenant", "secret"));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => graph.SendMessageBatchAsync(cts.Token));
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    private class RetryHandler : HttpMessageHandler {
        public int CallCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            CallCount++;
            if (CallCount < 3) {
                throw new HttpRequestException("fail");
            }
            var json = "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(json)
            });
        }
    }

    private class RetryAfterHandler : HttpMessageHandler {
        public int CallCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            CallCount++;
            if (request.RequestUri!.AbsoluteUri.Contains("oauth2")) {
                if (CallCount == 1) {
                    var resp = new HttpResponseMessage((HttpStatusCode)429);
                    resp.Headers.Add("Retry-After", "0");
                    return Task.FromResult(resp);
                }
                var json = "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private class AlwaysFailHandler : HttpMessageHandler {
        public int CallCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var json = "{\"error\":\"fail\"}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(json) });
        }
    }

    private class HangingHandler : HttpMessageHandler {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Unreachable");
        }
    }

    private class TransientFailHandler : HttpMessageHandler {
        public int CallCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            CallCount++;
            throw new HttpRequestException("fail");
        }
    }

    [Fact]
    public async Task ConnectO365GraphWithRetryAsync_RetriesUntilSuccess() {
        var handler = new RetryHandler();
        var field = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)field.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        var cacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cache = (System.Collections.Concurrent.ConcurrentDictionary<string, GraphAuthorization>)cacheField.GetValue(null)!;
        cache.Clear();
        OAuthCacheTestHelper.ResetOAuthTokenCache();
        OAuthCacheTestHelper.DeleteOAuthCacheFile();
        try {
            var credential = new GraphCredential { ClientId = "id", ClientSecret = "secret", DirectoryId = "tenant" };
            string token = await MicrosoftGraphUtils.ConnectO365GraphWithRetryAsync(credential, "tenant", 2, 0, 1);
            Assert.Equal("Bearer token", token);
            Assert.Equal(3, handler.CallCount);
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public async Task ConnectO365GraphWithRetryAsync_NoRetriesThrowsException() {
        var handler = new AlwaysFailHandler();
        var field = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)field.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        var cacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cache = (System.Collections.Concurrent.ConcurrentDictionary<string, GraphAuthorization>)cacheField.GetValue(null)!;
        cache.Clear();
        OAuthCacheTestHelper.ResetOAuthTokenCache();
        OAuthCacheTestHelper.DeleteOAuthCacheFile();
        try {
            var credential = new GraphCredential { ClientId = "id", ClientSecret = "secret", DirectoryId = "tenant" };
            await Assert.ThrowsAsync<GraphApiException>(() => MicrosoftGraphUtils.ConnectO365GraphWithRetryAsync(credential, "tenant", 0, 0, 1));
            Assert.Equal(1, handler.CallCount);
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public async Task ConnectO365GraphAsync_RetriesAfter429() {
        var handler = new RetryAfterHandler();
        var field = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)field.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        var cacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cache = (System.Collections.Concurrent.ConcurrentDictionary<string, GraphAuthorization>)cacheField.GetValue(null)!;
        cache.Clear();
        OAuthCacheTestHelper.ResetOAuthTokenCache();
        OAuthCacheTestHelper.DeleteOAuthCacheFile();
        try {
            var credential = new GraphCredential { ClientId = "id", ClientSecret = "secret", DirectoryId = "tenant" };
            string token = await MicrosoftGraphUtils.ConnectO365GraphAsync(credential, "tenant", "https://graph.microsoft.com");
            Assert.Equal("Bearer token", token);
            Assert.Equal(2, handler.CallCount);
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public async Task ConnectO365GraphAsync_CancellationRequested_Throws() {
        var handler = new HangingHandler();
        var field = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)field.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        var cacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cache = (System.Collections.Concurrent.ConcurrentDictionary<string, GraphAuthorization>)cacheField.GetValue(null)!;
        cache.Clear();
        OAuthCacheTestHelper.ResetOAuthTokenCache();
        OAuthCacheTestHelper.DeleteOAuthCacheFile();
        var cts = new CancellationTokenSource(100);
        try {
            var credential = new GraphCredential { ClientId = "id", ClientSecret = "secret", DirectoryId = "tenant" };
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MicrosoftGraphUtils.ConnectO365GraphAsync(credential, "tenant", "https://graph.microsoft.com", cts.Token));
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public async Task ConnectO365GraphWithRetryAsync_CancelledDuringDelay_Throws() {
        var handler = new TransientFailHandler();
        var field = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)field.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        var cacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cache = (System.Collections.Concurrent.ConcurrentDictionary<string, GraphAuthorization>)cacheField.GetValue(null)!;
        cache.Clear();
        OAuthCacheTestHelper.ResetOAuthTokenCache();
        OAuthCacheTestHelper.DeleteOAuthCacheFile();
        var cts = new CancellationTokenSource(100);
        try {
            var credential = new GraphCredential { ClientId = "id", ClientSecret = "secret", DirectoryId = "tenant" };
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => MicrosoftGraphUtils.ConnectO365GraphWithRetryAsync(credential, "tenant", 3, 10000, 1, "https://graph.microsoft.com", cts.Token));
        } finally {
            handlerField.SetValue(client, original);
        }
    }
}
