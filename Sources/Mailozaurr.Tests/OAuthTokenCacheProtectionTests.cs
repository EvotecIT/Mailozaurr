using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mailozaurr.Tests;

public sealed class OAuthTokenCacheProtectionTests {
    public OAuthTokenCacheProtectionTests() {
        ResetCache();
        DeleteCacheFile();
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

        var path = GetCacheFilePath();
        Assert.True(File.Exists(path));

        var json = File.ReadAllText(path);
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

        ResetCache();
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

        var path = GetCacheFilePath();
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

    private static void ResetCache() {
        var field = typeof(OAuthTokenCache).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic);
        field?.SetValue(null, null);
    }

    private static string GetCacheFilePath() {
        var pathField = typeof(OAuthTokenCache).GetField("CacheFilePath", BindingFlags.Static | BindingFlags.NonPublic);
        return (string)pathField!.GetValue(null)!;
    }

    private static void DeleteCacheFile() {
        var path = GetCacheFilePath();
        if (File.Exists(path)) {
            File.Delete(path);
        }
    }
}
