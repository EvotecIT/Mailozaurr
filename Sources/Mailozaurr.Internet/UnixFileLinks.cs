using System.Runtime.InteropServices;

namespace Mailozaurr;

/// <summary>Shared atomic no-replace link primitive for attachment and message-file commits.</summary>
internal static class UnixFileLinks {
    internal static int Create(string source, string destination) {
        int currentDirectory = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? -2 : -100;
        return Create(currentDirectory, source, currentDirectory, destination);
    }

    internal static int Create(int sourceDirectory, string source, int destinationDirectory, string destination) =>
        linkat(sourceDirectory, source, destinationDirectory, destination, 0);

    [DllImport("libc", SetLastError = true)]
    private static extern int linkat(int sourceDirectory, string source, int destinationDirectory, string destination, int flags);
}
