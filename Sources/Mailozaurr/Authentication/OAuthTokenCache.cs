using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Mailozaurr;

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

    public static OAuthCredential? Get(string key) {
        var cache = LoadCache();
        cache.TryGetValue(key, out var cred);
        return cred;
    }

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
