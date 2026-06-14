using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Mailozaurr.Tests;

public sealed class TokenCacheHelperTests : IDisposable {
    private readonly string _cachePath;

    public TokenCacheHelperTests() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", "TokenCache", Process.GetCurrentProcess().Id.ToString(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _cachePath = Path.Combine(directory, "msal_cache.bin");
        Environment.SetEnvironmentVariable("MAILOZAURR_MSAL_CACHE_PATH", _cachePath);
        DeleteCacheFile();
    }

    public void Dispose() {
        DeleteCacheFile();
        var directory = Path.GetDirectoryName(_cachePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)) {
            Directory.Delete(directory, recursive: true);
        }
        Environment.SetEnvironmentVariable("MAILOZAURR_MSAL_CACHE_PATH", null);
    }

    [Fact]
    public async Task ReadCacheData_RetriesTransientFileLockAndReturnsBytes() {
        var path = GetCacheFilePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }

        var expected = Encoding.UTF8.GetBytes("cached-token-data");
        File.WriteAllBytes(path, expected);

        var method = typeof(TokenCacheHelper).GetMethod("ReadCacheData", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var lockStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        try {
            var releaseTask = Task.Run(async () => {
                await Task.Delay(75);
                lockStream.Dispose();
            });

            var actual = (byte[]?)method!.Invoke(null, null);
            await releaseTask;

            Assert.NotNull(actual);
            Assert.Equal(expected, actual);
        } finally {
            lockStream.Dispose();
        }
    }

    [Fact]
    public async Task WriteCacheData_RetriesTransientFileLockAndPersistsBytes() {
        var path = GetCacheFilePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, new byte[] { 0x00 });

        var method = typeof(TokenCacheHelper).GetMethod("WriteCacheData", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var expected = Encoding.UTF8.GetBytes("updated-token-data");
        var lockStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        try {
            var releaseTask = Task.Run(async () => {
                await Task.Delay(75);
                lockStream.Dispose();
            });

            method!.Invoke(null, new object[] { expected });
            await releaseTask;

            Assert.Equal(expected, File.ReadAllBytes(path));
        } finally {
            lockStream.Dispose();
        }
    }

    private static string GetCacheFilePath() {
        return Environment.GetEnvironmentVariable("MAILOZAURR_MSAL_CACHE_PATH")!;
    }

    private static void DeleteCacheFile() {
        var path = GetCacheFilePath();
        if (File.Exists(path)) {
            File.Delete(path);
        }
    }
}