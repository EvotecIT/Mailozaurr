using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public class GraphMessageListenerTests {
    private class QueueHandler : HttpMessageHandler {
        private readonly Queue<(HttpResponseMessage Response, Action<HttpRequestMessage>? Callback)> _responses;

        public QueueHandler(IEnumerable<HttpResponseMessage> responses)
            : this(responses.Select(response => (response, (Action<HttpRequestMessage>?)null))) {
        }

        public QueueHandler(IEnumerable<(HttpResponseMessage Response, Action<HttpRequestMessage>? Callback)> responses) {
            _responses = new Queue<(HttpResponseMessage, Action<HttpRequestMessage>?)>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (_responses.Count > 0) {
                var (response, callback) = _responses.Dequeue();
                callback?.Invoke(request);
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{\"value\":[]}")
            });
        }
    }

    private static FieldInfo GetHandlerField() =>
        typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance) ??
        typeof(HttpMessageInvoker).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance) ??
        throw new InvalidOperationException("HttpClient handler field not found");

    private static HttpResponseMessage CreateTokenResponse() =>
        new(HttpStatusCode.OK) {
            Content = new StringContent("{\"access_token\":\"token\",\"token_type\":\"Bearer\",\"expires_in\":3600}")
        };

    private static HttpResponseMessage CreateMessagesResponse(params string[] ids) {
        var payload = string.Join(",", ids.Select(id => $"{{\"id\":\"{id}\"}}"));
        var json = $"{{\"value\":[{payload}]}}";
        return new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json)
        };
    }

    private static (HttpClient Client, FieldInfo HandlerField, HttpMessageHandler OriginalHandler) OverrideHttpClient(HttpMessageHandler handler) {
        var clientField = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)clientField.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        return (client, handlerField, original);
    }

    private static void ResetGraphCaches() {
        var cacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cache = (ConcurrentDictionary<string, GraphAuthorization>)cacheField.GetValue(null)!;
        cache.Clear();
        var oauthType = typeof(MicrosoftGraphUtils).Assembly.GetType("Mailozaurr.OAuthTokenCache");
        var oauthField = oauthType?.GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static);
        oauthField?.SetValue(null, null);
        string cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mailozaurr", "oauth_cache.json");
        if (File.Exists(cachePath)) {
            File.Delete(cachePath);
        }
    }

    [Fact]
    public async Task Listener_StartStopMultipleTimes_DoesNotLeakResources() {
        var responses = new[] {
            CreateTokenResponse(),
            CreateMessagesResponse(),
            CreateTokenResponse(),
            CreateMessagesResponse()
        };
        var handler = new QueueHandler(responses);
        var overrideInfo = OverrideHttpClient(handler);
        ResetGraphCaches();
        try {
            var cred = new GraphCredential { ClientId = "id", ClientSecret = "secret", DirectoryId = "tenant" };
            var listener = new GraphMessageListener(cred, "user", TimeSpan.FromSeconds(1));
            var cancelField = typeof(GraphMessageListener).GetField("_cancel", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var pollField = typeof(GraphMessageListener).GetField("_pollTask", BindingFlags.NonPublic | BindingFlags.Instance)!;

            await listener.StartAsync();
            var first = cancelField.GetValue(listener);
            var firstPoll = pollField.GetValue(listener);
            await Task.Delay(20);
            listener.Dispose();
            Assert.Null(cancelField.GetValue(listener));
            Assert.Null(pollField.GetValue(listener));

            await listener.StartAsync();
            var second = cancelField.GetValue(listener);
            var secondPoll = pollField.GetValue(listener);
            await Task.Delay(20);
            listener.Dispose();
            Assert.Null(cancelField.GetValue(listener));
            Assert.Null(pollField.GetValue(listener));

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotSame(first, second);
            Assert.NotNull(firstPoll);
            Assert.NotNull(secondPoll);
            Assert.NotSame(firstPoll, secondPoll);
        } finally {
            overrideInfo.HandlerField.SetValue(overrideInfo.Client, overrideInfo.OriginalHandler);
        }
    }

    [Fact]
    public async Task Listener_EvictsOldSeenIds_WhenCapacityExceeded() {
        var responses = new[] {
            CreateTokenResponse(),
            CreateMessagesResponse("old1", "old2"),
            CreateTokenResponse(),
            CreateMessagesResponse("new1"),
            CreateTokenResponse(),
            CreateMessagesResponse("new2"),
            CreateTokenResponse(),
            CreateMessagesResponse("old1")
        };

        var handler = new QueueHandler(responses);
        var overrideInfo = OverrideHttpClient(handler);
        ResetGraphCaches();

        GraphMessageListener? listener = null;
        try {
            var cred = new GraphCredential { ClientId = "id", ClientSecret = "secret", DirectoryId = "tenant" };
            var options = new GraphMessageListenerRetentionOptions { MaxSeenIds = 2 };
            listener = new GraphMessageListener(cred, "user", TimeSpan.FromMilliseconds(10), options);
            var ids = new List<string>();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.MessageArrived += (_, msg) => {
                if (msg.TryGetValue("id", out var idObj) && idObj is string id) {
                    lock (ids) {
                        ids.Add(id);
                        if (ids.Count >= 3) {
                            tcs.TrySetResult(true);
                        }
                    }
                }
            };

            await listener.StartAsync();
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            listener.Dispose();

            Assert.Same(tcs.Task, completed);
            Assert.Equal(new[] { "new1", "new2", "old1" }, ids);
        } finally {
            listener?.Dispose();
            overrideInfo.HandlerField.SetValue(overrideInfo.Client, overrideInfo.OriginalHandler);
        }
    }

    [Fact]
    public async Task Listener_EvictsExpiredIds_WhenSlidingWindowElapsed() {
        var responses = new[] {
            CreateTokenResponse(),
            CreateMessagesResponse("old1"),
            CreateTokenResponse(),
            CreateMessagesResponse("new1"),
            CreateTokenResponse(),
            CreateMessagesResponse("old1")
        };

        var handler = new QueueHandler(responses);
        var overrideInfo = OverrideHttpClient(handler);
        ResetGraphCaches();

        GraphMessageListener? listener = null;
        try {
            var cred = new GraphCredential { ClientId = "id", ClientSecret = "secret", DirectoryId = "tenant" };
            var options = new GraphMessageListenerRetentionOptions {
                MaxSeenIds = null,
                SlidingExpiration = TimeSpan.FromMilliseconds(50)
            };
            listener = new GraphMessageListener(cred, "user", TimeSpan.FromMilliseconds(100), options);
            var ids = new List<string>();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.MessageArrived += (_, msg) => {
                if (msg.TryGetValue("id", out var idObj) && idObj is string id) {
                    lock (ids) {
                        ids.Add(id);
                        if (ids.Count >= 2) {
                            tcs.TrySetResult(true);
                        }
                    }
                }
            };

            await listener.StartAsync();
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            listener.Dispose();

            Assert.Same(tcs.Task, completed);
            Assert.Equal(new[] { "new1", "old1" }, ids);
        } finally {
            listener?.Dispose();
            overrideInfo.HandlerField.SetValue(overrideInfo.Client, overrideInfo.OriginalHandler);
        }
    }

    [Fact]
    public async Task Listener_RaisesExpiredId_WhenItReturnsWithoutOtherTraffic() {
        var baseTime = DateTimeOffset.UtcNow;
        var currentTime = baseTime;
        var responses = new[] {
            (CreateTokenResponse(), (Action<HttpRequestMessage>?)null),
            (CreateMessagesResponse("old1"), (Action<HttpRequestMessage>?)(_ => currentTime = baseTime)),
            (CreateTokenResponse(), (Action<HttpRequestMessage>?)(_ => currentTime = baseTime.AddMilliseconds(10))),
            (CreateMessagesResponse(), (Action<HttpRequestMessage>?)(_ => currentTime = baseTime.AddMilliseconds(10))),
            (CreateTokenResponse(), (Action<HttpRequestMessage>?)(_ => currentTime = baseTime.AddMilliseconds(75))),
            (CreateMessagesResponse("old1"), (Action<HttpRequestMessage>?)(_ => currentTime = baseTime.AddMilliseconds(75)))
        };

        var handler = new QueueHandler(responses);
        var overrideInfo = OverrideHttpClient(handler);
        ResetGraphCaches();

        GraphMessageListener? listener = null;
        try {
            var cred = new GraphCredential { ClientId = "id", ClientSecret = "secret", DirectoryId = "tenant" };
            var options = new GraphMessageListenerRetentionOptions {
                MaxSeenIds = null,
                SlidingExpiration = TimeSpan.FromMilliseconds(50)
            };
            options.Clock = () => currentTime;
            listener = new GraphMessageListener(cred, "user", TimeSpan.FromMilliseconds(20), options);
            var ids = new List<string>();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.MessageArrived += (_, msg) => {
                if (msg.TryGetValue("id", out var idObj) && idObj is string id) {
                    lock (ids) {
                        ids.Add(id);
                        if (ids.Count >= 1) {
                            tcs.TrySetResult(true);
                        }
                    }
                }
            };

            await listener.StartAsync();
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            listener.Dispose();

            Assert.Same(tcs.Task, completed);
            Assert.Equal(new[] { "old1" }, ids);
        } finally {
            listener?.Dispose();
            overrideInfo.HandlerField.SetValue(overrideInfo.Client, overrideInfo.OriginalHandler);
        }
    }

    [Fact]
    public void Listener_Throws_WhenRetentionHasNoLimits() {
        var cred = new GraphCredential { ClientId = "id", ClientSecret = "secret", DirectoryId = "tenant" };
        var options = new GraphMessageListenerRetentionOptions {
            MaxSeenIds = null,
            SlidingExpiration = null
        };

        var ex = Assert.Throws<ArgumentException>(() => new GraphMessageListener(cred, "user", TimeSpan.FromSeconds(1), options));
        Assert.Equal("retentionOptions", ex.ParamName);
    }
}
}