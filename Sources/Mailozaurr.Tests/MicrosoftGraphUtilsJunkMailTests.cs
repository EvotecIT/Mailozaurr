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
public sealed class MicrosoftGraphUtilsJunkMailTests {
    [Fact]
    public void FilterJunkMessages_SingleUseSkipEnumerables_AppliesFiltersAcrossMultipleMessages() {
        var skipFrom = new SingleUseEnumerable<string>("skip-from@example.com");
        var skipTo = new SingleUseEnumerable<string>("skip-to@example.com");
        var skipSubjectContains = new SingleUseEnumerable<string>("urgent");

        var first = new Dictionary<string, object> {
            ["id"] = "1",
            ["from"] = new Dictionary<string, object> {
                ["emailAddress"] = new Dictionary<string, object> { ["address"] = "keep@example.com" }
            },
            ["toRecipients"] = new object[] {
                new Dictionary<string, object> {
                    ["emailAddress"] = new Dictionary<string, object> { ["address"] = "keep-recipient@example.com" }
                }
            },
            ["subject"] = "normal"
        };
        var second = new Dictionary<string, object> {
            ["id"] = "2",
            ["from"] = new Dictionary<string, object> {
                ["emailAddress"] = new Dictionary<string, object> { ["address"] = "skip-from@example.com" }
            },
            ["toRecipients"] = new object[] {
                new Dictionary<string, object> {
                    ["emailAddress"] = new Dictionary<string, object> { ["address"] = "skip-to@example.com" }
                }
            },
            ["subject"] = "urgent message"
        };

        var filtered = MicrosoftGraphUtils.FilterJunkMessages(
            new[] { first, second },
            skipFrom: skipFrom,
            skipTo: skipTo,
            skipSubjectContains: skipSubjectContains);

        var remaining = Assert.Single(filtered);
        Assert.Same(first, remaining);
        Assert.Equal(1, skipFrom.EnumerationCount);
        Assert.Equal(1, skipTo.EnumerationCount);
        Assert.Equal(1, skipSubjectContains.EnumerationCount);
    }

    [Fact]
    public async Task GetJunkMailMessagesAsync_SingleUseSkipAttachmentExtensions_AppliesFilter() {
        var handler = new JunkMailHandler();
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
            var skipExtensions = new SingleUseEnumerable<string>(".pdf");

            var results = await MicrosoftGraphUtils.GetJunkMailMessagesAsync(
                credential,
                "user@example.com",
                skipAttachmentExtension: skipExtensions);

            var remaining = Assert.Single(results);
            Assert.Equal("message-keep", remaining["id"]);
            Assert.Equal(1, skipExtensions.EnumerationCount);
            Assert.Equal(1, handler.AttachmentRequestCount);
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    private static FieldInfo GetHandlerField()
        => typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("HttpClient handler field not found");

    private sealed class JunkMailHandler : HttpMessageHandler {
        public int AttachmentRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var uri = request.RequestUri!;
            if (uri.AbsoluteUri.IndexOf("oauth2", StringComparison.Ordinal) >= 0) {
                var json = "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(json)
                });
            }

            if (uri.AbsolutePath.EndsWith("/mailFolders/junkemail/messages", StringComparison.Ordinal)) {
                const string json = "{\"value\":[{\"id\":\"message-drop\",\"hasAttachments\":true},{\"id\":\"message-keep\",\"hasAttachments\":false}]}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(json)
                });
            }

            if (uri.AbsolutePath.EndsWith("/messages/message-drop/attachments", StringComparison.Ordinal)) {
                AttachmentRequestCount++;
                const string json = "{\"value\":[{\"name\":\"invoice.pdf\"}]}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(json)
                });
            }

            if (uri.AbsolutePath.EndsWith("/messages/message-keep/attachments", StringComparison.Ordinal)) {
                AttachmentRequestCount++;
                const string json = "{\"value\":[{\"name\":\"note.txt\"}]}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(json)
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class SingleUseEnumerable<T> : IEnumerable<T> {
        private readonly IReadOnlyList<T> items;

        public SingleUseEnumerable(params T[] items) {
            this.items = items;
        }

        public int EnumerationCount { get; private set; }

        public IEnumerator<T> GetEnumerator() {
            EnumerationCount++;
            if (EnumerationCount > 1) {
                throw new InvalidOperationException("Sequence was enumerated more than once.");
            }

            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
