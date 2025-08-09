using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

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

    private static async Task<Dictionary<string, OAuthCredential>> LoadCacheAsync() {
        if (_cache != null) {
            return _cache;
        }
        Dictionary<string, OAuthCredential> cache;
        if (File.Exists(CacheFilePath)) {
#if NETFRAMEWORK || NETSTANDARD2_0
            string json;
            using (var stream = new FileStream(CacheFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (var reader = new StreamReader(stream)) {
                json = await reader.ReadToEndAsync().ConfigureAwait(false);
            }
#else
            var json = await File.ReadAllTextAsync(CacheFilePath).ConfigureAwait(false);
#endif
            cache = JsonSerializer.Deserialize<Dictionary<string, OAuthCredential>>(json) ?? new();
        } else {
            cache = new Dictionary<string, OAuthCredential>();
        }
        lock (LockObj) {
            _cache ??= cache;
            return _cache;
        }
    }

    /// <summary>
    /// Retrieves a credential from the cache.
    /// </summary>
    /// <param name="key">Unique cache key.</param>
    /// <returns>The cached credential or <c>null</c> if not found.</returns>
    public static async Task<OAuthCredential?> GetAsync(string key) {
        var cache = await LoadCacheAsync().ConfigureAwait(false);
        lock (LockObj) {
            cache.TryGetValue(key, out var cred);
            return cred;
        }
    }

    /// <summary>
    /// Saves a credential to the cache on disk.
    /// </summary>
    /// <param name="key">Unique cache key.</param>
    /// <param name="credential">Credential to cache.</param>
    public static async Task SetAsync(string key, OAuthCredential credential) {
        var cache = await LoadCacheAsync().ConfigureAwait(false);
        string? dir;
        string json;
        lock (LockObj) {
            cache[key] = credential;
            dir = Path.GetDirectoryName(CacheFilePath);
            if (!Directory.Exists(dir)) {
                Directory.CreateDirectory(dir!);
            }
            json = JsonSerializer.Serialize(cache);
        }
#if NETFRAMEWORK || NETSTANDARD2_0
        using (var stream = new FileStream(CacheFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        using (var writer = new StreamWriter(stream)) {
            await writer.WriteAsync(json).ConfigureAwait(false);
        }
#else
        await File.WriteAllTextAsync(CacheFilePath, json).ConfigureAwait(false);
#endif
    }
}
