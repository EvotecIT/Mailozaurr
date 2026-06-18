using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Mailozaurr.Tests;

internal static class OAuthCacheTestHelper {
    private const string OAuthCachePathEnvironmentVariable = "MAILOZAURR_OAUTH_CACHE_PATH";

    static OAuthCacheTestHelper() {
        Environment.SetEnvironmentVariable(OAuthCachePathEnvironmentVariable, GetTestCacheFilePath());
    }

    internal static void ResetOAuthTokenCache() {
        var field = typeof(OAuthTokenCache).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic);
        field?.SetValue(null, null);
    }

    internal static string GetOAuthCacheFilePath() {
        var pathProperty = typeof(OAuthTokenCache).GetProperty("CacheFilePath", BindingFlags.Static | BindingFlags.NonPublic);
        return (string)pathProperty!.GetValue(null)!;
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

    internal static string ReadOAuthCacheFileText() {
        var path = GetOAuthCacheFilePath();
        for (var attempt = 0; ; attempt++) {
            try {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            } catch (IOException) when (attempt < 4) {
                Thread.Sleep(25 * (attempt + 1));
            } catch (UnauthorizedAccessException) when (attempt < 4) {
                Thread.Sleep(25 * (attempt + 1));
            }
        }
    }

    private static string GetTestCacheFilePath() {
        var targetName = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Name;
        return Path.Combine(
            Path.GetTempPath(),
            "Mailozaurr.Tests",
            targetName,
            "oauth_cache.json");
    }
}
