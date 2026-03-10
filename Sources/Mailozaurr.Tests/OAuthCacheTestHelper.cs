using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Mailozaurr.Tests;

internal static class OAuthCacheTestHelper {
    internal static void ResetOAuthTokenCache() {
        var field = typeof(OAuthTokenCache).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic);
        field?.SetValue(null, null);
    }

    internal static string GetOAuthCacheFilePath() {
        var pathField = typeof(OAuthTokenCache).GetField("CacheFilePath", BindingFlags.Static | BindingFlags.NonPublic);
        return (string)pathField!.GetValue(null)!;
    }

    internal static void DeleteOAuthCacheFile() {
        var path = GetOAuthCacheFilePath();
        for (var attempt = 0; ; attempt++) {
            try {
                if (File.Exists(path)) {
                    File.Delete(path);
                }

                return;
            } catch (IOException) when (attempt < 4) {
                Thread.Sleep(25 * (attempt + 1));
            } catch (UnauthorizedAccessException) when (attempt < 4) {
                Thread.Sleep(25 * (attempt + 1));
            }
        }
    }
}
