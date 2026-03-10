using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class OAuthHelpersGoogleCachedTokenTests {
    public OAuthHelpersGoogleCachedTokenTests() {
        ResetCache();
        DeleteCacheFile();
    }

    [Fact]
    public async Task AcquireGoogleTokenCachedAsync_PrefersCompositeKey_WhenBothExist() {
        var account = "user@example.com";
        var clientId = "client-abc";
        var compositeKey = $"google:{clientId}:{account}";
        var legacyKey = $"google:{account}";

        var composite = new OAuthCredential {
            UserName = account,
            AccessToken = "composite-token",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
            ClientId = clientId,
            ClientSecret = "secret"
        };
        var legacy = new OAuthCredential {
            UserName = account,
            AccessToken = "legacy-token",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };

        await OAuthTokenCache.SetAsync(compositeKey, composite);
        await OAuthTokenCache.SetAsync(legacyKey, legacy);

        var result = await OAuthHelpers.AcquireGoogleTokenCachedAsync(account, clientId, "secret", Array.Empty<string>());

        Assert.Equal("composite-token", result.AccessToken);
        Assert.Equal(clientId, result.ClientId);
    }

    [Fact]
    public async Task AcquireGoogleTokenCachedAsync_FillsMissingClientData_AndMigratesLegacy() {
        var account = "user2@example.com";
        var clientId = "client-xyz";
        var legacyKey = $"google:{account}";
        var compositeKey = $"google:{clientId}:{account}";

        var legacy = new OAuthCredential {
            UserName = account,
            AccessToken = "legacy-token",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };
        await OAuthTokenCache.SetAsync(legacyKey, legacy);

        var result = await OAuthHelpers.AcquireGoogleTokenCachedAsync(account, clientId, "top-secret", Array.Empty<string>());

        Assert.Equal("legacy-token", result.AccessToken);
        Assert.Equal(clientId, result.ClientId);
        Assert.Equal("top-secret", result.ClientSecret);

        // Legacy entry should be migrated to composite key for future lookups
        var migrated = await OAuthTokenCache.GetAsync(compositeKey);
        Assert.NotNull(migrated);
        Assert.Equal("legacy-token", migrated!.AccessToken);
    }

    [Fact]
    public async Task PersistGoogleCredentialAsync_WritesLegacyAndCompositeEntries() {
        var account = "user3@example.com";
        var clientId = "client-123";
        var compositeKey = $"google:{clientId}:{account}";
        var legacyKey = $"google:{account}";
        var credential = new OAuthCredential {
            UserName = account,
            AccessToken = "persisted-token",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };

        await PersistGoogleCredentialAsync(credential, account, clientId);

        var composite = await OAuthTokenCache.GetAsync(compositeKey);
        var legacy = await OAuthTokenCache.GetAsync(legacyKey);

        Assert.NotNull(composite);
        Assert.NotNull(legacy);
        Assert.Equal("persisted-token", composite!.AccessToken);
        Assert.Equal("persisted-token", legacy!.AccessToken);
        Assert.Equal(clientId, legacy.ClientId);
    }

    private static void ResetCache() {
        var field = typeof(OAuthTokenCache).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic);
        field?.SetValue(null, null);
    }

    private static async Task PersistGoogleCredentialAsync(
        OAuthCredential credential,
        string gmailAccount,
        string clientId) {
        var method = typeof(OAuthHelpers).GetMethod("PersistGoogleCredentialAsync", BindingFlags.Static | BindingFlags.NonPublic);
        var task = (Task)method!.Invoke(null, new object[] { credential, gmailAccount, clientId })!;
        await task.ConfigureAwait(false);
    }

    private static void DeleteCacheFile() {
        var pathField = typeof(OAuthTokenCache).GetField("CacheFilePath", BindingFlags.Static | BindingFlags.NonPublic);
        var path = pathField?.GetValue(null) as string;
        if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
            File.Delete(path);
        }
    }
}
