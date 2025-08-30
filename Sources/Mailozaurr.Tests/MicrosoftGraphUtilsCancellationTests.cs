using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class MicrosoftGraphUtilsCancellationTests
{
    [Fact]
    public async Task ConnectO365GraphAsync_CancelledToken_Throws()
    {
        var credential = new GraphCredential { ClientId = "id", ClientSecret = "secret", DirectoryId = "tenant" };
        var cacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cache = (System.Collections.Concurrent.ConcurrentDictionary<string, GraphAuthorization>)cacheField.GetValue(null)!;
        cache.Clear();
        var oauthType = typeof(MicrosoftGraphUtils).Assembly.GetType("Mailozaurr.OAuthTokenCache");
        var oauthField = oauthType?.GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static);
        oauthField?.SetValue(null, null);
        string cachePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mailozaurr", "oauth_cache.json");
        if (System.IO.File.Exists(cachePath)) System.IO.File.Delete(cachePath);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(() => MicrosoftGraphUtils.ConnectO365GraphAsync(credential, "tenant", "https://graph.microsoft.com", cts.Token));
    }

    private static FieldInfo GetHandlerField()
        => typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("HttpClient handler field not found");

    private class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("fail");
        }
    }

    [Fact]
    public async Task ConnectO365GraphWithRetryAsync_CancellationDuringDelay_Throws()
    {
        var handler = new FailingHandler();
        var field = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)field.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        var cacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cache = (System.Collections.Concurrent.ConcurrentDictionary<string, GraphAuthorization>)cacheField.GetValue(null)!;
        cache.Clear();
        var oauthType = typeof(MicrosoftGraphUtils).Assembly.GetType("Mailozaurr.OAuthTokenCache");
        var oauthField = oauthType?.GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static);
        oauthField?.SetValue(null, null);
        string cachePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mailozaurr", "oauth_cache.json");
        if (System.IO.File.Exists(cachePath)) System.IO.File.Delete(cachePath);
        try
        {
            var credential = new GraphCredential { ClientId = "id", ClientSecret = "secret", DirectoryId = "tenant" };
            using var cts = new CancellationTokenSource(100);
            await Assert.ThrowsAsync<TaskCanceledException>(() => MicrosoftGraphUtils.ConnectO365GraphWithRetryAsync(credential, "tenant", 3, 1000, 1, "https://graph.microsoft.com", cts.Token));
        }
        finally
        {
            handlerField.SetValue(client, original);
        }
    }
}
