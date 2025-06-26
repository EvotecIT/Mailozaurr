using System;
using System.IO;
using Microsoft.Identity.Client;

namespace Mailozaurr;

internal static class TokenCacheHelper {
    private static readonly object FileLock = new();
    private static readonly string CacheFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mailozaurr",
        "msal_cache.bin");

    public static void RegisterCache(ITokenCache tokenCache) {
        tokenCache.SetBeforeAccess(BeforeAccessNotification);
        tokenCache.SetAfterAccess(AfterAccessNotification);
    }

    private static void BeforeAccessNotification(TokenCacheNotificationArgs args) {
        lock (FileLock) {
            if (File.Exists(CacheFilePath)) {
                var data = File.ReadAllBytes(CacheFilePath);
                args.TokenCache.DeserializeMsalV3(data, shouldClearExistingCache: true);
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
