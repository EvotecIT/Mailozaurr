using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
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

    private static Dictionary<string, OAuthCredential> ConvertCacheEntries(
        Dictionary<string, OAuthCredentialCacheEntry>? cacheEntries,
        ICredentialProtector protector) {
        var cache = new Dictionary<string, OAuthCredential>(StringComparer.Ordinal);
        if (cacheEntries == null) {
            return cache;
        }

        foreach (var pair in cacheEntries) {
            if (pair.Value == null) {
                continue;
            }

            cache[pair.Key] = pair.Value.ToCredential(protector);
        }

        return cache;
    }

    private static Dictionary<string, OAuthCredentialCacheEntry> CreateCacheEntries(
        Dictionary<string, OAuthCredential> cache,
        ICredentialProtector protector) {
        var entries = new Dictionary<string, OAuthCredentialCacheEntry>(StringComparer.Ordinal);
        foreach (var pair in cache) {
            if (pair.Value == null) {
                continue;
            }

            entries[pair.Key] = OAuthCredentialCacheEntry.FromCredential(pair.Value, protector);
        }

        return entries;
    }

    private static async Task<Dictionary<string, OAuthCredential>> LoadCacheAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        if (_cache != null) {
            cancellationToken.ThrowIfCancellationRequested();
            return _cache;
        }

        Dictionary<string, OAuthCredential> cache;
        if (File.Exists(CacheFilePath)) {
            try {
#if NETFRAMEWORK || NETSTANDARD2_0
                var json = await ReadCacheFileAsync(cancellationToken).ConfigureAwait(false);
#else
                var json = await File.ReadAllTextAsync(CacheFilePath, cancellationToken).ConfigureAwait(false);
#endif
                var protector = CredentialProtection.Default;
                var cacheEntries = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.DictionaryStringOAuthCredentialCacheEntry);
                cache = ConvertCacheEntries(cacheEntries, protector);
            } catch (FileNotFoundException) {
                cache = new Dictionary<string, OAuthCredential>();
            } catch (DirectoryNotFoundException) {
                cache = new Dictionary<string, OAuthCredential>();
            } catch (JsonException) {
                cache = new Dictionary<string, OAuthCredential>();
            }
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
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The cached credential or <c>null</c> if not found.</returns>
    public static async Task<OAuthCredential?> GetAsync(string key, CancellationToken cancellationToken = default) {
        var cache = await LoadCacheAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
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
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public static async Task SetAsync(string key, OAuthCredential credential, CancellationToken cancellationToken = default) {
        var cache = await LoadCacheAsync(cancellationToken).ConfigureAwait(false);
        string? dir;
        string json;
        lock (LockObj) {
            cancellationToken.ThrowIfCancellationRequested();
            cache[key] = credential;
            dir = Path.GetDirectoryName(CacheFilePath);
            if (!Directory.Exists(dir)) {
                Directory.CreateDirectory(dir!);
            }
            var cacheEntries = CreateCacheEntries(cache, CredentialProtection.Default);
            json = JsonSerializer.Serialize(cacheEntries, MailozaurrJsonContext.Default.DictionaryStringOAuthCredentialCacheEntry);
        }
        cancellationToken.ThrowIfCancellationRequested();
#if NETFRAMEWORK || NETSTANDARD2_0
        await WriteCacheFileAsync(json, cancellationToken).ConfigureAwait(false);
#else
        await File.WriteAllTextAsync(CacheFilePath, json, cancellationToken).ConfigureAwait(false);
#endif
    }

#if NETFRAMEWORK || NETSTANDARD2_0
    private static async Task<string> ReadCacheFileAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        using (var stream = new FileStream(CacheFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
        using (cancellationToken.Register(() => stream.Dispose()))
        using (var reader = new StreamReader(stream))
        using (cancellationToken.Register(() => reader.Dispose())) {
            try {
                var json = await reader.ReadToEndAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return json;
            } catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) {
                throw new OperationCanceledException(cancellationToken);
            }
        }
    }

    private static async Task WriteCacheFileAsync(string json, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        using (var stream = new FileStream(CacheFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        using (cancellationToken.Register(() => stream.Dispose()))
        using (var writer = new StreamWriter(stream))
        using (cancellationToken.Register(() => writer.Dispose())) {
            try {
                await writer.WriteAsync(json).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            } catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) {
                throw new OperationCanceledException(cancellationToken);
            }
        }
    }
#endif
}
