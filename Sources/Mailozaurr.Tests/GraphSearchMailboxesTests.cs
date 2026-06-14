using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public class GraphSearchMailboxesTests {
    private static FieldInfo GetHandlerField()
        => typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("HttpClient handler field not found");

    [Fact]
    public async Task SearchMailboxesAsync_SingleUseMailboxEnumerable_IsEnumeratedOnce() {
        var handler = new SearchMailboxesHandler();
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
            var mailboxes = new SingleUseEnumerable<string>("first@example.com");

            var results = await MicrosoftGraphUtils.SearchMailboxesAsync(credential, mailboxes, "subject:test");

            var message = Assert.Single(results);
            Assert.Equal("first@example.com", message.UserPrincipalName);
            Assert.Equal("message-1", message.Id);
            Assert.Equal(1, mailboxes.EnumerationCount);
            Assert.Equal(1, handler.SearchRequestCount);
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public async Task SearchMailboxesAsync_EmptyMailboxList_SkipsGraphRequest() {
        var handler = new SearchMailboxesHandler();
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

            var results = await MicrosoftGraphUtils.SearchMailboxesAsync(credential, Array.Empty<string>(), "subject:test");

            Assert.Empty(results);
            Assert.Equal(0, handler.AuthRequestCount);
            Assert.Equal(0, handler.SearchRequestCount);
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    private sealed class SearchMailboxesHandler : HttpMessageHandler {
        public int AuthRequestCount { get; private set; }
        public int SearchRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var uri = request.RequestUri!;
            if (uri.AbsoluteUri.IndexOf("oauth2", StringComparison.Ordinal) >= 0) {
                AuthRequestCount++;
                var json = "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(json)
                });
            }

            if (uri.AbsolutePath.EndsWith("/search/query", StringComparison.Ordinal)) {
                SearchRequestCount++;
                const string json = "{\"value\":[{\"hitsContainers\":[{\"hits\":[{\"summary\":\"match\",\"resource\":{\"id\":\"message-1\",\"subject\":\"Test subject\"}}]}]}]}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(json)
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class SingleUseEnumerable<T> : IEnumerable<T> {
        private readonly IReadOnlyList<T> _items;

        public SingleUseEnumerable(params T[] items) {
            _items = items;
        }

        public int EnumerationCount { get; private set; }

        public IEnumerator<T> GetEnumerator() {
            EnumerationCount++;
            if (EnumerationCount > 1) {
                throw new InvalidOperationException("Sequence was enumerated more than once.");
            }

            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}