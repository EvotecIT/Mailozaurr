using System;
using System.IO;
using Microsoft.Identity.Client;

namespace Mailozaurr;

/// <summary>
/// Provides helper methods for persisting and retrieving MSAL token caches.
/// </summary>
internal static class TokenCacheHelper {
    private static readonly object FileLock = new();
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
            if (File.Exists(CacheFilePath)) {
                try {
                    var data = File.ReadAllBytes(CacheFilePath);
                    args.TokenCache.DeserializeMsalV3(data, shouldClearExistingCache: true);
                } catch (FileNotFoundException) {
                    // another thread/process deleted the cache between the existence check and read
                } catch (DirectoryNotFoundException) {
                    // treat a missing cache directory as an empty cache
                }
            }
        }
    }

    private static void AfterAccessNotification(TokenCacheNotificationArgs args) {
        if (args.HasStateChanged) {
            lock (FileLock) {
                var directory = Path.GetDirectoryName(CacheFilePath);
                if (!Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory!);
                }
                var data = args.TokenCache.SerializeMsalV3();
                File.WriteAllBytes(CacheFilePath, data);
            }
        }
    }
}
