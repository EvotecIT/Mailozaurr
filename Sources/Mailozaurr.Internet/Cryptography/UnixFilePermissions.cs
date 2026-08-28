using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Mailozaurr;

internal static class UnixFilePermissions {
    private const int UserReadWriteExecute = 0x1C0;
    private const int UserReadWrite = 0x180;

    internal static void RestrictDirectory(string path) {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return;
        }

        Chmod(path, UserReadWriteExecute);
    }

    internal static void RestrictFile(string path) {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return;
        }

        Chmod(path, UserReadWrite);
    }

    internal static FileStream OpenRestrictedFile(
        string path,
        FileMode mode,
        FileAccess access,
        FileShare share) {
        bool existed = File.Exists(path);
        FileStream? stream = null;
        try {
            stream = new FileStream(path, mode, access, share);
            RestrictFile(path);
            return stream;
        } catch {
            stream?.Dispose();
            if (!existed) {
                try {
                    if (File.Exists(path)) File.Delete(path);
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                }
            }
            throw;
        }
    }

    private static void Chmod(string path, int mode) {
        if (chmod(path, mode) != 0) {
            throw new IOException($"Failed to restrict permissions for '{path}'.", new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int chmod(string pathname, int mode);
}
