using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class OAuthHelpersCachedTokenTests {
    public OAuthHelpersCachedTokenTests() {
        ResetCache();
        DeleteCacheFile();
    }

    [Fact]
    public async Task AcquireO365TokenCachedAsync_ReturnsCachedToken() {
        var cacheKey = "o365:test@example.com";
        var credential = new OAuthCredential {
            UserName = "test@example.com",
            AccessToken = "token",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };
        await OAuthTokenCache.SetAsync(cacheKey, credential);

        var result = await OAuthHelpers.AcquireO365TokenCachedAsync(
            "test@example.com",
            "client-id",
            "tenant-id",
            "https://login.microsoftonline.com/common/oauth2/nativeclient",
            Array.Empty<string>());

        Assert.Equal(credential.AccessToken, result.AccessToken);
        Assert.Equal(credential.UserName, result.UserName);
    }

    [Fact]
    public async Task GetAsync_ThrowsWhenCancellationRequested() {
        var cacheKey = "o365:cancel@example.com";
        var credential = new OAuthCredential {
            UserName = "cancel@example.com",
            AccessToken = "token",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };
        await OAuthTokenCache.SetAsync(cacheKey, credential);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => OAuthTokenCache.GetAsync(cacheKey, cts.Token));
    }

    [Fact]
    public async Task SetAsync_ThrowsWhenCancellationRequested() {
        var cacheKey = "o365:cancel-set@example.com";
        var credential = new OAuthCredential {
            UserName = "cancel-set@example.com",
            AccessToken = "token",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => OAuthTokenCache.SetAsync(cacheKey, credential, cts.Token));
    }

    private static void ResetCache() {
        var field = typeof(OAuthTokenCache).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic);
        field?.SetValue(null, null);
    }

    private static void DeleteCacheFile() {
        var pathField = typeof(OAuthTokenCache).GetField("CacheFilePath", BindingFlags.Static | BindingFlags.NonPublic);
        var path = pathField?.GetValue(null) as string;
        if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
            File.Delete(path);
        }
    }
}

