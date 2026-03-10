using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Mailozaurr.Tests;

public sealed class TokenCacheHelperTests {
    public TokenCacheHelperTests() {
        DeleteCacheFile();
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
        var pathField = typeof(TokenCacheHelper).GetField("CacheFilePath", BindingFlags.Static | BindingFlags.NonPublic);
        return (string)pathField!.GetValue(null)!;
    }

    private static void DeleteCacheFile() {
        var path = GetCacheFilePath();
        if (File.Exists(path)) {
            File.Delete(path);
        }
    }
}
