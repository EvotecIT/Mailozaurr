using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Mailozaurr;

/// <summary>Binds attachment creation and commit to a validated directory object.</summary>
internal sealed class AttachmentDirectoryLease : IDisposable {
    private const uint FileReadAttributes = 0x80;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const int LinuxAtSymlinkNoFollow = 0x100;
    private const int DarwinAtSymlinkNoFollow = 0x0020;
    private const int DarwinAtRemoveDirectory = 0x0080;
    private const int LinuxAtRemoveDirectory = 0x0200;
    private const uint RenameExchange = 0x00000002;
    private const uint UnixFileTypeMask = 0xF000;
    private const uint UnixRegularFile = 0x8000;
    private readonly List<SafeFileHandle> _windowsHandles = new();
    private SafeFileHandle? _unixDirectory;
    private SafeFileHandle? _unixTemporaryDirectory;
    private string? _unixTemporaryDirectoryName;

    private AttachmentDirectoryLease(string directoryPath) => DirectoryPath = directoryPath;

    internal string DirectoryPath { get; }
    internal bool UsesNativeRelativePaths => !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    internal static AttachmentDirectoryLease Acquire(string directory) {
        string fullPath = Path.GetFullPath(directory);
        var lease = new AttachmentDirectoryLease(fullPath);
        try {
            if (lease.UsesNativeRelativePaths) lease.AcquireUnix();
            else lease.AcquireWindows();
            return lease;
        } catch {
            lease.Dispose();
            throw;
        }
    }

    internal FileStream CreateTemporaryFile(bool useAsync, out string path) =>
        CreateTemporaryFile(useAsync, out path, out _);

    internal FileStream CreateTemporaryFile(
        bool useAsync,
        out string path,
        out string? cleanupDirectoryPath) {
        using var random = RandomNumberGenerator.Create();
        var bytes = new byte[16];
        cleanupDirectoryPath = null;
        for (int attempt = 0; attempt < 128; attempt++) {
            random.GetBytes(bytes);
            string name = ".mailozaurr-attachment-" +
                BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant() + ".tmp";
            path = Path.Combine(DirectoryPath, name);
            if (!UsesNativeRelativePaths) {
                try {
                    return new FileStream(
                        path,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        64 * 1024,
                        FileOptions.WriteThrough | (useAsync ? FileOptions.Asynchronous : 0));
                } catch (IOException) when (AttachmentFileStore.PathEntryExistsForLease(path)) {
                    continue;
                }
            }

            int temporaryDirectoryFd = GetTemporaryDirectoryFileDescriptor(out cleanupDirectoryPath);
            int fd = openat(
                temporaryDirectoryFd,
                name,
                UnixOpenWriteFlags(),
                Convert.ToUInt32("600", 8));
            if (fd >= 0) {
                var handle = new SafeFileHandle(new IntPtr(fd), ownsHandle: true);
                if (fchmod(fd, Convert.ToUInt32("600", 8)) != 0) {
                    int error = Marshal.GetLastWin32Error();
                    handle.Dispose();
                    unlinkat(temporaryDirectoryFd, name, 0);
                    throw new IOException($"Unable to restrict a temporary attachment file (errno {error}).");
                }
                path = cleanupDirectoryPath == null
                    ? Path.Combine(DirectoryPath, name)
                    : Path.Combine(cleanupDirectoryPath, name);
                // POSIX descriptors do not use Windows overlapped-I/O metadata. FileStream still
                // provides its asynchronous API over this synchronous, directory-bound handle.
                return new FileStream(handle, FileAccess.Write, 64 * 1024, isAsync: false);
            }
            if (Marshal.GetLastWin32Error() != 17) ThrowUnixIOException("create a temporary attachment file");
        }
        path = string.Empty;
        throw new IOException("Unable to allocate a temporary attachment file.");
    }

    internal AttachmentFileSaveResult Commit(
        string temporaryPath,
        string destinationPath,
        AttachmentFileConflictPolicy conflictPolicy) {
        if (!UsesNativeRelativePaths) throw new InvalidOperationException("Native relative commit is only used on Unix.");
        string temporaryName = Path.GetFileName(temporaryPath);
        string destinationName = Path.GetFileName(destinationPath);
        int directoryFd = _unixDirectory!.DangerousGetHandle().ToInt32();
        int temporaryDirectoryFd = GetTemporaryDirectoryFileDescriptor(out _);

        switch (conflictPolicy) {
            case AttachmentFileConflictPolicy.Fail:
                LinkTemporary(temporaryDirectoryFd, temporaryName, directoryFd, destinationName);
                return new AttachmentFileSaveResult(destinationPath, AttachmentFileSaveAction.Created);
            case AttachmentFileConflictPolicy.Skip:
                if (TryLinkTemporary(temporaryDirectoryFd, temporaryName, directoryFd, destinationName)) {
                    return new AttachmentFileSaveResult(destinationPath, AttachmentFileSaveAction.Created);
                }
                RejectUnixSymbolicLink(directoryFd, destinationName);
                return new AttachmentFileSaveResult(destinationPath, AttachmentFileSaveAction.Skipped);
            case AttachmentFileConflictPolicy.Rename:
                string extension = Path.GetExtension(destinationName);
                string stem = Path.GetFileNameWithoutExtension(destinationName);
                for (int suffix = 0; suffix < 10_000; suffix++) {
                    string candidate = suffix == 0
                        ? destinationName
                        : stem + " (" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")" + extension;
                    if (!TryLinkTemporary(temporaryDirectoryFd, temporaryName, directoryFd, candidate)) {
                        RejectUnixSymbolicLink(directoryFd, candidate);
                        continue;
                    }
                    return new AttachmentFileSaveResult(
                        Path.Combine(DirectoryPath, candidate),
                        suffix == 0 ? AttachmentFileSaveAction.Created : AttachmentFileSaveAction.Renamed);
                }
                throw new IOException("No collision-free attachment filename was available.");
            case AttachmentFileConflictPolicy.Replace:
                if (TryLinkTemporary(temporaryDirectoryFd, temporaryName, directoryFd, destinationName)) {
                    return new AttachmentFileSaveResult(destinationPath, AttachmentFileSaveAction.Created);
                }
                // Reject a stable non-regular entry before mutating either name. The
                // exchange and second validation below close a concurrent final-entry swap.
                RequireUnixRegularFile(directoryFd, destinationName);
                ExchangeUnixEntries(temporaryDirectoryFd, temporaryName, directoryFd, destinationName);
                try {
                    RequireUnixRegularFile(temporaryDirectoryFd, temporaryName);
                } catch (Exception validationFailure) {
                    try {
                        ExchangeUnixEntries(temporaryDirectoryFd, temporaryName, directoryFd, destinationName);
                    } catch (Exception rollbackFailure) {
                        throw new IOException(
                            "The attachment replacement target changed type and the atomic exchange could not be rolled back.",
                            new AggregateException(validationFailure, rollbackFailure));
                    }
                    throw;
                }
                return new AttachmentFileSaveResult(
                    destinationPath,
                    AttachmentFileSaveAction.Replaced);
            default:
                throw new ArgumentOutOfRangeException(nameof(conflictPolicy));
        }
    }

    internal void DeleteTemporary(string path) {
        if (!UsesNativeRelativePaths) {
            AttachmentFileStore.TryDeleteTemporaryFileForLease(path);
            return;
        }
        if (string.IsNullOrWhiteSpace(path)) return;
        int directoryFd = GetTemporaryDirectoryFileDescriptor(out _);
        string name = Path.GetFileName(path);
        try {
            if (!TryGetUnixFileMode(directoryFd, name, out uint mode) ||
                (mode & UnixFileTypeMask) != UnixRegularFile) return;
            unlinkat(directoryFd, name, 0);
        } catch (IOException) {
            // Cleanup must never unlink an entry that another process substituted.
        }
    }

    internal bool TrySkipExisting(string destinationPath) {
        if (!UsesNativeRelativePaths) {
            throw new InvalidOperationException("Native relative inspection is only used on Unix.");
        }
        int directoryFd = _unixDirectory!.DangerousGetHandle().ToInt32();
        string name = Path.GetFileName(destinationPath);
        if (!TryGetUnixFileMode(directoryFd, name, out uint mode)) return false;
        if ((mode & UnixFileTypeMask) != UnixRegularFile) {
            throw new IOException("Only regular files can be retained as existing attachment destinations.");
        }
        return true;
    }

    private void AcquireWindows() {
        string root = Path.GetPathRoot(DirectoryPath)
            ?? throw new IOException("Attachment directory has no filesystem root.");
        string current = root;
        OpenWindowsDirectory(current);
        string relative = DirectoryPath.Substring(root.Length);
        foreach (string component in relative.Split(
                     new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries)) {
            current = Path.Combine(current, component);
            if (!Directory.Exists(current)) {
                Directory.CreateDirectory(current);
                UnixFilePermissions.RestrictDirectory(current);
            }
            OpenWindowsDirectory(current);
        }
    }

    private void OpenWindowsDirectory(string path) {
        SafeFileHandle handle = CreateFileW(
            path,
            FileReadAttributes,
            FileShare.Read | FileShare.Write,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            IntPtr.Zero);
        if (handle.IsInvalid) {
            handle.Dispose();
            throw new IOException($"Unable to bind attachment directory '{path}'.", new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
        }
        if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information) ||
            (information.FileAttributes & FileAttributes.Directory) == 0 ||
            (information.FileAttributes & FileAttributes.ReparsePoint) != 0) {
            handle.Dispose();
            throw new IOException("Attachment destination directories cannot be symbolic links or reparse points.");
        }
        _windowsHandles.Add(handle);
    }

    private void AcquireUnix() {
        int flags = UnixOpenDirectoryFlags();
        int current = open("/", flags);
        if (current < 0) ThrowUnixIOException("open the filesystem root");
        var currentHandle = new SafeFileHandle(new IntPtr(current), ownsHandle: true);
        try {
            string leasePath = NormalizeDarwinSystemAlias(DirectoryPath);
            foreach (string component in leasePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)) {
                int next = openat(currentHandle.DangerousGetHandle().ToInt32(), component, flags, 0);
                if (next < 0 && Marshal.GetLastWin32Error() == 2) {
                    if (mkdirat(
                            currentHandle.DangerousGetHandle().ToInt32(),
                            component,
                            Convert.ToUInt32("700", 8)) != 0 && Marshal.GetLastWin32Error() != 17) {
                        ThrowUnixIOException("create an attachment directory");
                    }
                    next = openat(currentHandle.DangerousGetHandle().ToInt32(), component, flags, 0);
                }
                if (next < 0) ThrowUnixIOException("open an attachment directory without following links");
                var nextHandle = new SafeFileHandle(new IntPtr(next), ownsHandle: true);
                currentHandle.Dispose();
                currentHandle = nextHandle;
            }
            _unixDirectory = currentHandle;
            currentHandle = null!;
        } finally {
            currentHandle?.Dispose();
        }
    }

    private int GetTemporaryDirectoryFileDescriptor(out string? cleanupDirectoryPath) {
        EnsureUnixTemporaryDirectory();
        cleanupDirectoryPath = Path.Combine(DirectoryPath, _unixTemporaryDirectoryName!);
        return _unixTemporaryDirectory!.DangerousGetHandle().ToInt32();
    }

    private void EnsureUnixTemporaryDirectory() {
        if (_unixTemporaryDirectory != null) return;
        int parentDirectoryFd = _unixDirectory!.DangerousGetHandle().ToInt32();
        using var random = RandomNumberGenerator.Create();
        var bytes = new byte[16];
        for (int attempt = 0; attempt < 128; attempt++) {
            random.GetBytes(bytes);
            string name = ".mailozaurr-staging-" +
                BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            if (mkdirat(parentDirectoryFd, name, Convert.ToUInt32("700", 8)) != 0) {
                if (Marshal.GetLastWin32Error() == 17) continue;
                ThrowUnixIOException("create a private attachment staging directory");
            }

            int fd = openat(parentDirectoryFd, name, UnixOpenDirectoryFlags(), 0);
            if (fd < 0) {
                int error = Marshal.GetLastWin32Error();
                unlinkat(parentDirectoryFd, name, UnixAtRemoveDirectoryFlag());
                throw new IOException($"Unable to bind a private attachment staging directory (errno {error}).");
            }
            if (fchmod(fd, Convert.ToUInt32("700", 8)) != 0) {
                int error = Marshal.GetLastWin32Error();
                new SafeFileHandle(new IntPtr(fd), ownsHandle: true).Dispose();
                unlinkat(parentDirectoryFd, name, UnixAtRemoveDirectoryFlag());
                throw new IOException($"Unable to restrict a private attachment staging directory (errno {error}).");
            }

            _unixTemporaryDirectory = new SafeFileHandle(new IntPtr(fd), ownsHandle: true);
            _unixTemporaryDirectoryName = name;
            return;
        }
        throw new IOException("Unable to allocate a private attachment staging directory.");
    }

    /// <summary>
    /// Resolves only macOS's fixed root aliases before the no-follow walk. Darwin exposes
    /// <c>/etc</c>, <c>/tmp</c>, and <c>/var</c> as links into <c>/private</c>; in particular,
    /// <see cref="Path.GetTempPath"/> normally returns a path beneath <c>/var</c>. All
    /// caller-controlled descendants are still opened component-by-component with
    /// <c>O_NOFOLLOW</c>.
    /// </summary>
    private static string NormalizeDarwinSystemAlias(string path) {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return path;
        foreach (string alias in new[] { "/etc", "/tmp", "/var" }) {
            if (string.Equals(path, alias, StringComparison.Ordinal) ||
                path.StartsWith(alias + "/", StringComparison.Ordinal)) {
                return "/private" + path;
            }
        }
        return path;
    }

    private static bool TryLinkTemporary(
        int temporaryDirectoryFd,
        string temporaryName,
        int destinationDirectoryFd,
        string destinationName) {
        if (linkat(temporaryDirectoryFd, temporaryName, destinationDirectoryFd, destinationName, 0) == 0) {
            if (unlinkat(temporaryDirectoryFd, temporaryName, 0) != 0) ThrowUnixIOException("remove a committed temporary attachment link");
            return true;
        }
        int error = Marshal.GetLastWin32Error();
        if (error == 17) return false;
        ThrowUnixIOException("commit an attachment file");
        return false;
    }

    private static void LinkTemporary(
        int temporaryDirectoryFd,
        string temporaryName,
        int destinationDirectoryFd,
        string destinationName) {
        if (!TryLinkTemporary(temporaryDirectoryFd, temporaryName, destinationDirectoryFd, destinationName)) {
            throw new IOException("The attachment destination already exists.");
        }
    }

    private static void ExchangeUnixEntries(
        int temporaryDirectoryFd,
        string temporaryName,
        int destinationDirectoryFd,
        string destinationName) {
        int result;
        try {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                try {
                    result = renameat2(temporaryDirectoryFd, temporaryName, destinationDirectoryFd, destinationName, RenameExchange);
                } catch (EntryPointNotFoundException) {
                    long syscallNumber = RuntimeInformation.ProcessArchitecture switch {
                        Architecture.X64 => 316,
                        Architecture.Arm64 => 276,
                        _ => throw new PlatformNotSupportedException(
                            $"Atomic attachment replacement is not supported on Linux architecture '{RuntimeInformation.ProcessArchitecture}'.")
                    };
                    result = checked((int)syscallRenameAt2(
                        syscallNumber,
                        temporaryDirectoryFd,
                        temporaryName,
                        destinationDirectoryFd,
                        destinationName,
                        RenameExchange));
                }
            } else {
                result = renameatx_np(temporaryDirectoryFd, temporaryName, destinationDirectoryFd, destinationName, RenameExchange);
            }
        } catch (EntryPointNotFoundException exception) {
            throw new PlatformNotSupportedException(
                "Atomic attachment replacement requires an operating-system rename-exchange primitive.",
                exception);
        }
        if (result != 0) ThrowUnixIOException("atomically exchange the attachment destination");
    }

    private static void RejectUnixSymbolicLink(int directoryFd, string name) {
        var buffer = new byte[1];
        long result = readlinkat(directoryFd, name, buffer, new UIntPtr(1));
        if (result >= 0) {
            throw new IOException("Attachment destinations cannot be symbolic links or reparse points.");
        }
        int error = Marshal.GetLastWin32Error();
        if (error == 22 || error == 2) return;
        ThrowUnixIOException("inspect an attachment destination without following links");
    }

    /// <summary>
    /// Verifies the replacement target through the leased directory without following links.
    /// Native <c>stat</c> layouts expose the file-type bits at architecture-specific offsets.
    /// Unsupported Unix architectures fail closed rather than replacing an unclassified entry.
    /// </summary>
    private static void RequireUnixRegularFile(int directoryFd, string name) {
        if (!TryGetUnixFileMode(directoryFd, name, out uint mode)) {
            throw new IOException("The attachment replacement target no longer exists.");
        }
        if ((mode & UnixFileTypeMask) != UnixRegularFile) {
            throw new IOException("Only regular files can be replaced as attachment destinations.");
        }
    }

    private static bool TryGetUnixFileMode(int directoryFd, string name, out uint mode) {
        const int statBufferSize = 256;
        IntPtr statBuffer = Marshal.AllocHGlobal(statBufferSize);
        try {
            int noFollowFlag = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? DarwinAtSymlinkNoFollow
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? LinuxAtSymlinkNoFollow
                    : throw UnsupportedUnixPlatform();
            if (fstatat(directoryFd, name, statBuffer, noFollowFlag) != 0) {
                if (Marshal.GetLastWin32Error() == 2) {
                    mode = 0;
                    return false;
                }
                ThrowUnixIOException("inspect an attachment replacement target without following links");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                mode = unchecked((ushort)Marshal.ReadInt16(statBuffer, 4));
            } else {
                int modeOffset = RuntimeInformation.ProcessArchitecture switch {
                    Architecture.X64 => 24,
                    Architecture.Arm64 => 16,
                    _ => throw new PlatformNotSupportedException(
                        $"Atomic attachment replacement is not supported on Unix architecture '{RuntimeInformation.ProcessArchitecture}'.")
                };
                mode = unchecked((uint)Marshal.ReadInt32(statBuffer, modeOffset));
            }

            return true;
        } finally {
            Marshal.FreeHGlobal(statBuffer);
        }
    }

    private static int UnixOpenDirectoryFlags() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? 0x100000 | 0x100 | 0x1000000
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? 0x10000 | 0x20000 | 0x80000
            : throw UnsupportedUnixPlatform();

    private static int UnixOpenWriteFlags() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? 0x0001 | 0x0200 | 0x0800 | 0x0100 | 0x1000000
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? 0x0001 | 0x0040 | 0x0080 | 0x20000 | 0x80000
            : throw UnsupportedUnixPlatform();

    private static int UnixAtRemoveDirectoryFlag() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? DarwinAtRemoveDirectory
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? LinuxAtRemoveDirectory
            : throw UnsupportedUnixPlatform();

    private static PlatformNotSupportedException UnsupportedUnixPlatform() => new(
        $"Secure attachment filesystem operations are supported only on Windows, Linux, and macOS; '{RuntimeInformation.OSDescription}' is not supported.");

    private static void ThrowUnixIOException(string operation) =>
        throw new IOException($"Unable to {operation} (errno {Marshal.GetLastWin32Error()}).");

    public void Dispose() {
        _unixTemporaryDirectory?.Dispose();
        _unixTemporaryDirectory = null;
        if (_unixTemporaryDirectoryName != null && _unixDirectory != null && !_unixDirectory.IsInvalid) {
            unlinkat(
                _unixDirectory.DangerousGetHandle().ToInt32(),
                _unixTemporaryDirectoryName,
                UnixAtRemoveDirectoryFlag());
        }
        _unixTemporaryDirectoryName = null;
        _unixDirectory?.Dispose();
        _unixDirectory = null;
        foreach (SafeFileHandle handle in _windowsHandles) handle.Dispose();
        _windowsHandles.Clear();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

    [DllImport("libc", SetLastError = true)] private static extern int open(string path, int flags);
    [DllImport("libc", SetLastError = true)] private static extern int openat(int directoryFd, string path, int flags, uint mode);
    [DllImport("libc", SetLastError = true)] private static extern int mkdirat(int directoryFd, string path, uint mode);
    [DllImport("libc", SetLastError = true)] private static extern int fchmod(int fileDescriptor, uint mode);
    [DllImport("libc", SetLastError = true)] private static extern int linkat(int oldDirectoryFd, string oldPath, int newDirectoryFd, string newPath, int flags);
    [DllImport("libc", SetLastError = true)] private static extern int unlinkat(int directoryFd, string path, int flags);
    [DllImport("libc", SetLastError = true)] private static extern int renameat2(int oldDirectoryFd, string oldPath, int newDirectoryFd, string newPath, uint flags);
    [DllImport("libc", SetLastError = true)] private static extern int renameatx_np(int oldDirectoryFd, string oldPath, int newDirectoryFd, string newPath, uint flags);
    [DllImport("libc", EntryPoint = "syscall", SetLastError = true)] private static extern long syscallRenameAt2(long number, int oldDirectoryFd, string oldPath, int newDirectoryFd, string newPath, uint flags);
    [DllImport("libc", SetLastError = true)] private static extern long readlinkat(int directoryFd, string path, byte[] buffer, UIntPtr bufferSize);
    [DllImport("libc", SetLastError = true)] private static extern int fstatat(int directoryFd, string path, IntPtr statBuffer, int flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation {
        internal FileAttributes FileAttributes;
        private readonly System.Runtime.InteropServices.ComTypes.FILETIME _creationTime;
        private readonly System.Runtime.InteropServices.ComTypes.FILETIME _lastAccessTime;
        private readonly System.Runtime.InteropServices.ComTypes.FILETIME _lastWriteTime;
        private readonly uint _volumeSerialNumber;
        private readonly uint _fileSizeHigh;
        private readonly uint _fileSizeLow;
        private readonly uint _numberOfLinks;
        private readonly uint _fileIndexHigh;
        private readonly uint _fileIndexLow;
    }

}
