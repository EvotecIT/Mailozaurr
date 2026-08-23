using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public class OAuthHelpersGoogleCachedTokenTests {
    public OAuthHelpersGoogleCachedTokenTests() {
        OAuthCacheTestHelper.ResetOAuthTokenCache();
        OAuthCacheTestHelper.DeleteOAuthCacheFile();
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

        var result = await GmailOAuthHelpers.AcquireTokenCachedAsync(account, clientId, "secret", Array.Empty<string>());

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

        var result = await GmailOAuthHelpers.AcquireTokenCachedAsync(account, clientId, "top-secret", Array.Empty<string>());

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

    [Fact]
    public async Task AcquireGoogleTokenCachedAsync_PartitionsCredentialsByRequestedScopes() {
        var account = "scoped@example.com";
        var clientId = "client-scoped";
        var defaultScopes = new[] { "https://www.googleapis.com/auth/gmail.modify" };
        var featureScopes = new[] {
            "https://www.googleapis.com/auth/gmail.modify",
            "https://www.googleapis.com/auth/gmail.settings.basic"
        };
        var defaultKey = BuildScopedCacheKey(account, clientId, defaultScopes);
        var featureKey = BuildScopedCacheKey(account, clientId, featureScopes);
        Assert.NotEqual(defaultKey, featureKey);

        await OAuthTokenCache.SetAsync(defaultKey, new OAuthCredential {
            UserName = account,
            AccessToken = "default-token",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
            ClientId = clientId
        });
        await OAuthTokenCache.SetAsync(featureKey, new OAuthCredential {
            UserName = account,
            AccessToken = "feature-token",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
            ClientId = clientId
        });

        var defaultResult = await GmailOAuthHelpers.AcquireTokenCachedAsync(account, clientId, "secret", defaultScopes);
        var featureResult = await GmailOAuthHelpers.AcquireTokenCachedAsync(account, clientId, "secret", featureScopes);

        Assert.Equal("default-token", defaultResult.AccessToken);
        Assert.Equal("feature-token", featureResult.AccessToken);
    }

    private static async Task PersistGoogleCredentialAsync(
        OAuthCredential credential,
        string gmailAccount,
        string clientId) {
        var method = typeof(GmailOAuthHelpers).GetMethod(
            "PersistCredentialAsync",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(OAuthCredential), typeof(string), typeof(string) },
            modifiers: null);
        var task = (Task)method!.Invoke(null, new object[] { credential, gmailAccount, clientId })!;
        await task.ConfigureAwait(false);
    }

    private static string BuildScopedCacheKey(string gmailAccount, string clientId, IEnumerable<string> scopes) {
        var method = typeof(GmailOAuthHelpers).GetMethod(
            "BuildCacheKey",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(string), typeof(string), typeof(IEnumerable<string>) },
            modifiers: null);
        return (string)method!.Invoke(null, new object[] { gmailAccount, clientId, scopes })!;
    }

}
