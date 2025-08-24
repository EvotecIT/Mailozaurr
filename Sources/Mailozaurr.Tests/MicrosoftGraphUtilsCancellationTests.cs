using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Mailozaurr;
using Xunit;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public class MicrosoftGraphUtilsCancellationTests {
    private static FieldInfo GetHandlerField() =>
        typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("HttpClient handler field not found");

    [Fact]
    public async Task GetMailMessagesAsync_HonorsCancellation() {
        var response = new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"value\":[{\"id\":\"1\"}]}")
        };
        var handler = new RecordingHandler(response);
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
        var cts = new CancellationTokenSource();
        cts.Cancel();
        try {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MicrosoftGraphUtils.GetMailMessagesAsync(cred, "u", cancellationToken: cts.Token));
        } finally {
            handlerField.SetValue(client, original);
            tokenCache.TryRemove(key, out _);
        }
    }

    [Fact]
    public async Task GetMailMessageMimeAsync_HonorsCancellation() {
        var response = new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("From: a@b.com\r\nTo: c@d.com\r\nSubject: t\r\n\r\nbody")
        };
        var handler = new RecordingHandler(response);
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
        var cts = new CancellationTokenSource();
        cts.Cancel();
        try {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MicrosoftGraphUtils.GetMailMessageMimeAsync(cred, "u", "1", cts.Token));
        } finally {
            handlerField.SetValue(client, original);
            tokenCache.TryRemove(key, out _);
        }
    }
}
