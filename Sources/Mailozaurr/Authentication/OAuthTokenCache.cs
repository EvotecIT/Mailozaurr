using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Mailozaurr;

/// <summary>
/// Provides simple persistent caching for OAuth credentials.
/// </summary>
internal static class OAuthTokenCache {
    private static readonly object LockObj = new();
    private static readonly string CacheFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mailozaurr",
        "oauth_cache.json");

    private static Dictionary<string, OAuthCredential>? _cache;

    private static Dictionary<string, OAuthCredential> LoadCache() {
        lock (LockObj) {
            if (_cache != null) {
                return _cache;
            }
            if (File.Exists(CacheFilePath)) {
                var json = File.ReadAllText(CacheFilePath);
                _cache = JsonSerializer.Deserialize<Dictionary<string, OAuthCredential>>(json) ?? new();
            } else {
                _cache = new Dictionary<string, OAuthCredential>();
            }
            return _cache;
        }
    }

    /// <summary>
    /// Retrieves a credential from the cache.
    /// </summary>
    /// <param name="key">Unique cache key.</param>
    /// <returns>The cached credential or <c>null</c> if not found.</returns>
    public static OAuthCredential? Get(string key) {
        var cache = LoadCache();
        cache.TryGetValue(key, out var cred);
        return cred;
    }

    /// <summary>
    /// Saves a credential to the cache on disk.
    /// </summary>
    /// <param name="key">Unique cache key.</param>
    /// <param name="credential">Credential to cache.</param>
    public static void Set(string key, OAuthCredential credential) {
        var cache = LoadCache();
        cache[key] = credential;
        lock (LockObj) {
            var dir = Path.GetDirectoryName(CacheFilePath);
            if (!Directory.Exists(dir)) {
                Directory.CreateDirectory(dir!);
            }
            var json = JsonSerializer.Serialize(cache);
            File.WriteAllText(CacheFilePath, json);
        }
    }
}
