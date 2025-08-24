using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Mailozaurr;
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
        var key = "id|tenant||secret|https://graph.microsoft.com";
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
}

