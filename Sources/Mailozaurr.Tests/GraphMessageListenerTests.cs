using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphMessageListenerTests {
    private class QueueHandler : HttpMessageHandler {
        private readonly Queue<HttpResponseMessage> _responses;

        public QueueHandler(IEnumerable<HttpResponseMessage> responses) {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (_responses.Count > 0) {
                return Task.FromResult(_responses.Dequeue());
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

    [Fact]
    public async Task Listener_StartStopMultipleTimes_DoesNotLeakResources() {
        var responses = new[] {
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"token\",\"token_type\":\"Bearer\"}") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"value\":[]}") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"token\",\"token_type\":\"Bearer\"}") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"value\":[]}") }
        };
        var handler = new QueueHandler(responses);
        var clientField = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)clientField.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        var cacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cache = (ConcurrentDictionary<string, GraphAuthorization>)cacheField.GetValue(null)!;
        cache.Clear();
        var oauthType = typeof(MicrosoftGraphUtils).Assembly.GetType("Mailozaurr.OAuthTokenCache");
        var oauthField = oauthType?.GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static);
        oauthField?.SetValue(null, null);
        string cachePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mailozaurr", "oauth_cache.json");
        if (System.IO.File.Exists(cachePath)) System.IO.File.Delete(cachePath);
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
            handlerField.SetValue(client, original);
        }
    }}