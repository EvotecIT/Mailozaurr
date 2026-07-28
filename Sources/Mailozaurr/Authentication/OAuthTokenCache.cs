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
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    private const int IoRetryCount = 5;
    private static readonly TimeSpan FileLockRetryDelay = TimeSpan.FromMilliseconds(25);
    private const string CachePathEnvironmentVariable = "MAILOZAURR_OAUTH_CACHE_PATH";
    private static string CacheFilePath => ResolveCacheFilePath();

    private static Dictionary<string, OAuthCredential>? _cache;

    private static Dictionary<string, OAuthCredential> ConvertCacheEntries(
        Dictionary<string, OAuthCredentialCacheEntry>? cacheEntries,
        ICredentialProtector protector,
        out bool removedUnsafeKeys) {
        var cache = new Dictionary<string, OAuthCredential>(StringComparer.Ordinal);
        removedUnsafeKeys = false;
        if (cacheEntries == null) {
            return cache;
        }

        foreach (var pair in cacheEntries) {
            if (IsUnsafeLegacyGraphCacheKey(pair.Key)) {
                removedUnsafeKeys = true;
                continue;
            }
            if (pair.Value == null) {
                continue;
            }

            cache[pair.Key] = pair.Value.ToCredential(protector);
        }

        return cache;
    }

    private static bool IsUnsafeLegacyGraphCacheKey(string key) =>
        key.StartsWith("graph:", StringComparison.Ordinal) &&
        !key.StartsWith("graph:v2:", StringComparison.Ordinal) &&
        key.IndexOf('|') >= 0;

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
        bool rewriteSanitizedCache = false;
        if (File.Exists(CacheFilePath)) {
            try {
                var json = await ReadCacheFileAsync(cancellationToken).ConfigureAwait(false);
                var protector = CredentialProtection.Default;
                var cacheEntries = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.DictionaryStringOAuthCredentialCacheEntry);
                cache = ConvertCacheEntries(cacheEntries, protector, out rewriteSanitizedCache);
            } catch (FileNotFoundException) {
                cache = new Dictionary<string, OAuthCredential>();
            } catch (DirectoryNotFoundException) {
                cache = new Dictionary<string, OAuthCredential>();
            } catch (JsonException) {
                cache = new Dictionary<string, OAuthCredential>();
            } catch (IOException) {
                cache = new Dictionary<string, OAuthCredential>();
            }
        } else {
            cache = new Dictionary<string, OAuthCredential>();
        }

        bool shouldRewrite;
        lock (LockObj) {
            shouldRewrite = _cache == null && rewriteSanitizedCache;
            _cache ??= cache;
            cache = _cache;
        }

        if (shouldRewrite) {
            await MutateAndPersistCacheAsync(static _ => { }, cancellationToken).ConfigureAwait(false);
        }

        return cache;
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
        await LoadCacheAsync(cancellationToken).ConfigureAwait(false);
        await MutateAndPersistCacheAsync(
            cache => cache[key] = credential,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task MutateAndPersistCacheAsync(
        Action<Dictionary<string, OAuthCredential>> mutation,
        CancellationToken cancellationToken) {
        await WriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            using var fileLock = await AcquireFileLockAsync(cancellationToken).ConfigureAwait(false);
            var cache = await ReadLatestCacheAsync(cancellationToken).ConfigureAwait(false);
            mutation(cache);
            cancellationToken.ThrowIfCancellationRequested();

            var cacheEntries = CreateCacheEntries(cache, CredentialProtection.Default);
            var json = JsonSerializer.Serialize(
                cacheEntries,
                MailozaurrJsonContext.Default.DictionaryStringOAuthCredentialCacheEntry);
            await WriteCacheFileAsync(json, cancellationToken).ConfigureAwait(false);

            lock (LockObj) {
                _cache = cache;
            }
        } finally {
            WriteGate.Release();
        }
    }

    private static async Task<Dictionary<string, OAuthCredential>> ReadLatestCacheAsync(
        CancellationToken cancellationToken) {
        if (!File.Exists(CacheFilePath)) {
            return new Dictionary<string, OAuthCredential>(StringComparer.Ordinal);
        }

        try {
            var json = await ReadCacheFileAsync(cancellationToken).ConfigureAwait(false);
            var entries = JsonSerializer.Deserialize(
                json,
                MailozaurrJsonContext.Default.DictionaryStringOAuthCredentialCacheEntry);
            return ConvertCacheEntries(entries, CredentialProtection.Default, out _);
        } catch (FileNotFoundException) {
            return new Dictionary<string, OAuthCredential>(StringComparer.Ordinal);
        } catch (DirectoryNotFoundException) {
            return new Dictionary<string, OAuthCredential>(StringComparer.Ordinal);
        } catch (JsonException) {
            return new Dictionary<string, OAuthCredential>(StringComparer.Ordinal);
        }
    }

    private static async Task<FileStream> AcquireFileLockAsync(CancellationToken cancellationToken) {
        var lockPath = CacheFilePath + ".lock";
        var directory = Path.GetDirectoryName(lockPath);
        if (string.IsNullOrWhiteSpace(directory)) {
            throw new InvalidOperationException("OAuth cache path is invalid.");
        }
        Directory.CreateDirectory(directory);

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.None);
            } catch (IOException) {
                await Task.Delay(FileLockRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<string> ReadCacheFileAsync(CancellationToken cancellationToken) {
        for (var attempt = 0; ; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                using (var stream = new FileStream(CacheFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, true))
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
            } catch (IOException) when (attempt < IoRetryCount - 1) {
                await Task.Delay(GetRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task WriteCacheFileAsync(string json, CancellationToken cancellationToken) {
        var directory = Path.GetDirectoryName(CacheFilePath);
        if (string.IsNullOrWhiteSpace(directory)) {
            throw new InvalidOperationException("OAuth cache path is invalid.");
        }

        for (var attempt = 0; ; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            var tempPath = Path.Combine(directory, Path.GetRandomFileName());
            try {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 4096, true))
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

                ReplaceCacheFile(tempPath);
                tempPath = string.Empty;
                return;
            } catch (IOException) when (attempt < IoRetryCount - 1) {
                await Task.Delay(GetRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            } finally {
                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath)) {
                    try {
                        File.Delete(tempPath);
                    } catch (IOException) {
                    }
                }
            }
        }
    }

    private static void ReplaceCacheFile(string tempPath) {
        if (File.Exists(CacheFilePath)) {
            try {
                File.Replace(tempPath, CacheFilePath, null, ignoreMetadataErrors: true);
                return;
            } catch (FileNotFoundException) {
            } catch (PlatformNotSupportedException) {
            }
        }

        if (File.Exists(CacheFilePath)) {
            File.Copy(tempPath, CacheFilePath, overwrite: true);
            File.Delete(tempPath);
            return;
        }

        try {
            File.Move(tempPath, CacheFilePath);
        } catch (IOException) when (File.Exists(CacheFilePath)) {
            File.Copy(tempPath, CacheFilePath, overwrite: true);
            File.Delete(tempPath);
        }
    }

    private static TimeSpan GetRetryDelay(int attempt) =>
        TimeSpan.FromMilliseconds(25 * (attempt + 1));

    private static string ResolveCacheFilePath() {
        var overriddenPath = Environment.GetEnvironmentVariable(CachePathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overriddenPath)) {
            return overriddenPath.Trim();
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Mailozaurr",
            "oauth_cache.json");
    }
}
