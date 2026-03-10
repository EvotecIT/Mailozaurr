using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public class OAuthHelpersCachedTokenTests {
    public OAuthHelpersCachedTokenTests() {
        OAuthCacheTestHelper.ResetOAuthTokenCache();
        OAuthCacheTestHelper.DeleteOAuthCacheFile();
    }

    [Fact]
    public async Task AcquireO365TokenCachedAsync_ReturnsCachedToken() {
        var cacheKey = BuildO365CacheKey(
            "test@example.com",
            "client-id",
            "tenant-id",
            "https://login.microsoftonline.com/common/oauth2/nativeclient",
            Array.Empty<string>());
        var credential = new OAuthCredential {
            UserName = "test@example.com",
            AccessToken = "token",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
            ClientId = "client-id"
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
    public async Task AcquireO365TokenCachedAsync_SeparatesEntriesPerClientAndScope() {
        var login = "test@example.com";
        var redirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient";
        var cacheKeyA = BuildO365CacheKey(login, "client-a", "tenant-id", redirectUri, new[] { "Mail.Read" });
        var cacheKeyB = BuildO365CacheKey(login, "client-b", "tenant-id", redirectUri, new[] { "Mail.Read", "offline_access" });

        await OAuthTokenCache.SetAsync(cacheKeyA, new OAuthCredential {
            UserName = login,
            AccessToken = "token-a",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
            ClientId = "client-a"
        });
        await OAuthTokenCache.SetAsync(cacheKeyB, new OAuthCredential {
            UserName = login,
            AccessToken = "token-b",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
            ClientId = "client-b"
        });

        var result = await OAuthHelpers.AcquireO365TokenCachedAsync(
            login,
            "client-b",
            "tenant-id",
            redirectUri,
            new[] { "offline_access", "Mail.Read" });

        Assert.Equal("token-b", result.AccessToken);
        Assert.Equal("client-b", result.ClientId);
    }

    [Fact]
    public async Task PersistO365CredentialAsync_WritesLegacyAndCompositeEntries() {
        var login = "persist@example.com";
        var redirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient";
        var credential = new OAuthCredential {
            UserName = login,
            AccessToken = "persist-token",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };

        await PersistO365CredentialAsync(
            credential,
            "client-id",
            "tenant-id",
            redirectUri,
            new[] { "offline_access", "Mail.Read" });

        var composite = await OAuthTokenCache.GetAsync(BuildO365CacheKey(login, "client-id", "tenant-id", redirectUri, new[] { "Mail.Read", "offline_access" }));
        var legacy = await OAuthTokenCache.GetAsync("o365:persist@example.com");

        Assert.NotNull(composite);
        Assert.NotNull(legacy);
        Assert.Equal("persist-token", composite!.AccessToken);
        Assert.Equal("persist-token", legacy!.AccessToken);
        Assert.Equal("client-id", legacy.ClientId);
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

    private static string BuildO365CacheKey(
        string login,
        string clientId,
        string tenantId,
        string redirectUri,
        string[] scopes) {
        var method = typeof(OAuthHelpers).GetMethod("BuildO365CacheKey", BindingFlags.Static | BindingFlags.NonPublic);
        return (string)method!.Invoke(null, new object[] { login, clientId, tenantId, redirectUri, scopes })!;
    }

    private static async Task PersistO365CredentialAsync(
        OAuthCredential credential,
        string clientId,
        string tenantId,
        string redirectUri,
        string[] scopes) {
        var method = typeof(OAuthHelpers).GetMethod("PersistO365CredentialAsync", BindingFlags.Static | BindingFlags.NonPublic);
        var task = (Task)method!.Invoke(null, new object[] { credential, clientId, tenantId, redirectUri, scopes })!;
        await task.ConfigureAwait(false);
    }

}
