using System.Runtime.InteropServices;

namespace Mailozaurr.Tests;

public sealed class MailFileMimeTemporaryStorageTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StagingFileIsPrivateAndDeletedOnClose(bool asynchronous) {
        FileStream stream = MailFileMimeTemporaryStorage.Create(asynchronous, out string path);
        try {
            Assert.True(File.Exists(path));
            Assert.Equal(asynchronous, stream.IsAsync);
#if NET8_0_OR_GREATER
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                UnixFileMode mode = File.GetUnixFileMode(path);
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
            }
#endif
            stream.WriteByte(42);
            stream.Position = 0;
            Assert.Equal(42, stream.ReadByte());
        } finally {
            stream.Dispose();
        }

        Assert.False(File.Exists(path));
    }
}
