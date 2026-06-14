using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public sealed class OAuthTokenCacheProtectionTests {
    public OAuthTokenCacheProtectionTests() {
        OAuthCacheTestHelper.ResetOAuthTokenCache();
        OAuthCacheTestHelper.DeleteOAuthCacheFile();
    }

    [Fact]
    public async Task SetAsync_WritesProtectedSecretsToDisk() {
        var cacheKey = "oauth:protected@example.com";
        var credential = new OAuthCredential {
            UserName = "protected@example.com",
            AccessToken = "access-token-value",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
            RefreshToken = "refresh-token-value",
            ClientId = "client-id",
            ClientSecret = "client-secret-value",
            ServiceAccountJson = "{\"type\":\"service_account\"}",
            ServiceAccountSubject = "subject@example.com"
        };

        await OAuthTokenCache.SetAsync(cacheKey, credential);

        var path = OAuthCacheTestHelper.GetOAuthCacheFilePath();
        Assert.True(File.Exists(path));

        var json = OAuthCacheTestHelper.ReadOAuthCacheFileText();
        Assert.DoesNotContain("access-token-value", json, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-token-value", json, StringComparison.Ordinal);
        Assert.DoesNotContain("client-secret-value", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"type\":\"service_account\"", json, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(json);
        var entry = document.RootElement.GetProperty(cacheKey);
        Assert.True(entry.TryGetProperty("AccessTokenProtected", out _));
        Assert.True(entry.TryGetProperty("RefreshTokenProtected", out _));
        Assert.True(entry.TryGetProperty("ClientSecretProtected", out _));
        Assert.True(entry.TryGetProperty("ServiceAccountJsonProtected", out _));

        OAuthCacheTestHelper.ResetOAuthTokenCache();
        var reloaded = await OAuthTokenCache.GetAsync(cacheKey);

        Assert.NotNull(reloaded);
        Assert.Equal(credential.AccessToken, reloaded!.AccessToken);
        Assert.Equal(credential.RefreshToken, reloaded.RefreshToken);
        Assert.Equal(credential.ClientSecret, reloaded.ClientSecret);
        Assert.Equal(credential.ServiceAccountJson, reloaded.ServiceAccountJson);
        Assert.Equal(credential.ServiceAccountSubject, reloaded.ServiceAccountSubject);
    }

    [Fact]
    public async Task GetAsync_LoadsLegacyPlaintextCacheFile() {
        var cacheKey = "oauth:legacy@example.com";
        var credential = new OAuthCredential {
            UserName = "legacy@example.com",
            AccessToken = "legacy-access-token",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(30),
            RefreshToken = "legacy-refresh-token",
            ClientId = "legacy-client",
            ClientSecret = "legacy-secret",
            ServiceAccountJson = "{\"legacy\":true}",
            ServiceAccountSubject = "legacy-subject@example.com"
        };

        var path = OAuthCacheTestHelper.GetOAuthCacheFilePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }

        var legacyCache = new Dictionary<string, OAuthCredential>(StringComparer.Ordinal) {
            [cacheKey] = credential
        };
        var json = JsonSerializer.Serialize(legacyCache, MailozaurrJsonContext.Default.DictionaryStringOAuthCredential);
        File.WriteAllText(path, json);

        var loaded = await OAuthTokenCache.GetAsync(cacheKey);

        Assert.NotNull(loaded);
        Assert.Equal(credential.UserName, loaded!.UserName);
        Assert.Equal(credential.AccessToken, loaded.AccessToken);
        Assert.Equal(credential.RefreshToken, loaded.RefreshToken);
        Assert.Equal(credential.ClientId, loaded.ClientId);
        Assert.Equal(credential.ClientSecret, loaded.ClientSecret);
        Assert.Equal(credential.ServiceAccountJson, loaded.ServiceAccountJson);
        Assert.Equal(credential.ServiceAccountSubject, loaded.ServiceAccountSubject);
    }

    [Fact]
    public async Task GetAsync_MalformedCacheFileReturnsNull() {
        var cacheKey = "oauth:malformed@example.com";
        var path = OAuthCacheTestHelper.GetOAuthCacheFilePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, "{broken");

        var loaded = await OAuthTokenCache.GetAsync(cacheKey);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task SetAsync_MalformedCacheFileIsRecovered() {
        var cacheKey = "oauth:recover@example.com";
        var path = OAuthCacheTestHelper.GetOAuthCacheFilePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, "{broken");

        var credential = new OAuthCredential {
            UserName = "recover@example.com",
            AccessToken = "recovered-access-token",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(30)
        };

        await OAuthTokenCache.SetAsync(cacheKey, credential);

        OAuthCacheTestHelper.ResetOAuthTokenCache();
        var loaded = await OAuthTokenCache.GetAsync(cacheKey);

        Assert.NotNull(loaded);
        Assert.Equal(credential.UserName, loaded!.UserName);
        Assert.Equal(credential.AccessToken, loaded.AccessToken);

        var json = OAuthCacheTestHelper.ReadOAuthCacheFileText();
        Assert.DoesNotContain("{broken", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_RetriesTransientFileLockAndLoadsCredential() {
        var cacheKey = "oauth:locked@example.com";
        var credential = new OAuthCredential {
            UserName = "locked@example.com",
            AccessToken = "locked-access-token",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(30)
        };

        var path = OAuthCacheTestHelper.GetOAuthCacheFilePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }

        var cacheEntries = new Dictionary<string, OAuthCredentialCacheEntry>(StringComparer.Ordinal) {
            [cacheKey] = OAuthCredentialCacheEntry.FromCredential(credential, CredentialProtection.Default)
        };
        var json = JsonSerializer.Serialize(cacheEntries, MailozaurrJsonContext.Default.DictionaryStringOAuthCredentialCacheEntry);
        File.WriteAllText(path, json);
        var lockStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        try {
            var releaseTask = Task.Run(async () => {
                await Task.Delay(75);
                lockStream.Dispose();
            });

            var loaded = await OAuthTokenCache.GetAsync(cacheKey);
            await releaseTask;

            Assert.NotNull(loaded);
            Assert.Equal(credential.AccessToken, loaded!.AccessToken);
        } finally {
            lockStream.Dispose();
        }
    }

}