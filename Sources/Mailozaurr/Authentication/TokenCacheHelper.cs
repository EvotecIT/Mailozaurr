using System;
using System.IO;
using System.Threading;
using Microsoft.Identity.Client;

namespace Mailozaurr;

/// <summary>
/// Provides helper methods for persisting and retrieving MSAL token caches.
/// </summary>
internal static class TokenCacheHelper {
    private static readonly object FileLock = new();
    private const int IoRetryCount = 5;
    private static readonly string CacheFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mailozaurr",
        "msal_cache.bin");

    /// <summary>
    /// Registers callbacks to persist the token cache before and after access.
    /// </summary>
    /// <param name="tokenCache">Token cache instance from MSAL.</param>
    public static void RegisterCache(ITokenCache tokenCache) {
        tokenCache.SetBeforeAccess(BeforeAccessNotification);
        tokenCache.SetAfterAccess(AfterAccessNotification);
    }

    private static void BeforeAccessNotification(TokenCacheNotificationArgs args) {
        lock (FileLock) {
            var data = ReadCacheData();
            if (data != null && data.Length > 0) {
                args.TokenCache.DeserializeMsalV3(data, shouldClearExistingCache: true);
            }
        }
    }

    private static void AfterAccessNotification(TokenCacheNotificationArgs args) {
        if (args.HasStateChanged) {
            lock (FileLock) {
                WriteCacheData(args.TokenCache.SerializeMsalV3());
            }
        }
    }

    private static byte[]? ReadCacheData() {
        if (!File.Exists(CacheFilePath)) {
            return null;
        }

        for (var attempt = 0; ; attempt++) {
            try {
                using var stream = new FileStream(CacheFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return buffer.ToArray();
            } catch (FileNotFoundException) {
                return null;
            } catch (DirectoryNotFoundException) {
                return null;
            } catch (IOException) when (attempt < IoRetryCount - 1) {
                Thread.Sleep(GetRetryDelay(attempt));
            } catch (IOException ex) {
                LoggingMessages.Logger.WriteWarning($"Failed to read MSAL token cache: {ex.Message}");
                return null;
            }
        }
    }

    private static void WriteCacheData(byte[] data) {
        var directory = Path.GetDirectoryName(CacheFilePath);
        if (string.IsNullOrWhiteSpace(directory)) {
            return;
        }

        if (!Directory.Exists(directory)) {
            Directory.CreateDirectory(directory!);
        }

        for (var attempt = 0; ; attempt++) {
            var tempPath = Path.Combine(directory, Path.GetRandomFileName());
            try {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) {
                    stream.Write(data, 0, data.Length);
                    stream.Flush(flushToDisk: true);
                }

                ReplaceCacheFile(tempPath);
                tempPath = string.Empty;
                return;
            } catch (IOException) when (attempt < IoRetryCount - 1) {
                Thread.Sleep(GetRetryDelay(attempt));
            } catch (IOException ex) {
                LoggingMessages.Logger.WriteWarning($"Failed to persist MSAL token cache: {ex.Message}");
                return;
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

    private static int GetRetryDelay(int attempt) => 25 * (attempt + 1);
}
