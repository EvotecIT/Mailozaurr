using OfficeIMO.Email;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Mailozaurr;

internal static class MailFileMimeTemporaryStorage {
    internal static FileStream Create(bool asynchronous) => Create(asynchronous, out _);

    internal static FileStream Create(bool asynchronous, out string path) {
        path = Path.Combine(Path.GetTempPath(),
            string.Concat("mailozaurr-mime-", Guid.NewGuid().ToString("N"), ".tmp"));
        FileOptions options = FileOptions.DeleteOnClose | FileOptions.SequentialScan;
        if (asynchronous) options |= FileOptions.Asynchronous;
#if NET8_0_OR_GREATER
        if (!OperatingSystem.IsWindows()) return CreateUnixOwnerOnly(path, asynchronous);
        var streamOptions = new FileStreamOptions {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            BufferSize = 81920,
            Options = options
        };
        return new FileStream(path, streamOptions);
#else
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return CreateUnixOwnerOnly(path, asynchronous);
        }
        return new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite,
            FileShare.None, 81920, options);
#endif
    }

    private static FileStream CreateUnixOwnerOnly(string path, bool asynchronous) {
        int descriptor = OpenFile(path, GetExclusiveCreateFlags(), 0x180U);
        if (descriptor < 0) {
            throw new IOException(
                "Unable to create an owner-only MIME staging file (OS error "
                + Marshal.GetLastWin32Error() + ").");
        }

        var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
        try {
            if (ChangeDescriptorMode(descriptor, 0x180U) != 0) {
                throw new IOException(
                    "Unable to secure the owner-only MIME staging file (OS error "
                    + Marshal.GetLastWin32Error() + ").");
            }
            FileOptions options = FileOptions.DeleteOnClose | FileOptions.SequentialScan;
            if (asynchronous) options |= FileOptions.Asynchronous;
            var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite,
                FileShare.None, 81920, options);
            handle.Dispose();
            handle = null!;
            return stream;
        } catch {
            handle?.Dispose();
            TryDelete(path);
            throw;
        }
    }

    private static int GetExclusiveCreateFlags() {
        const int openReadWrite = 0x0002;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            const int openCreate = 0x0200;
            const int openExclusive = 0x0800;
            return openReadWrite | openCreate | openExclusive;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            const int openCreate = 0x0040;
            const int openExclusive = 0x0080;
            return openReadWrite | openCreate | openExclusive;
        }
        throw new PlatformNotSupportedException(
            "This Unix platform does not expose a supported exclusive-create flag layout.");
    }

    private static void TryDelete(string path) {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int OpenFile(string path, int flags, uint mode);

    [DllImport("libc", EntryPoint = "fchmod", SetLastError = true)]
    private static extern int ChangeDescriptorMode(int descriptor, uint mode);
}

internal sealed class MailFileBoundedWriteStream : Stream {
    private readonly Stream _destination;
    private readonly long _maximumBytes;
    private long _bytesWritten;

    internal MailFileBoundedWriteStream(Stream destination, long maximumBytes) {
        _destination = destination ?? throw new ArgumentNullException(nameof(destination));
        if (!destination.CanWrite) throw new ArgumentException("The destination must be writable.", nameof(destination));
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        _maximumBytes = maximumBytes;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _bytesWritten;
    public override long Position {
        get => _bytesWritten;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _destination.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _destination.FlushAsync(cancellationToken);

    public override void Write(byte[] buffer, int offset, int count) {
        EnsureCapacity(count);
        _destination.Write(buffer, offset, count);
        _bytesWritten += count;
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureCapacity(count);
        await _destination.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        _bytesWritten += count;
    }

#if NET8_0_OR_GREATER
    public override void Write(ReadOnlySpan<byte> buffer) {
        EnsureCapacity(buffer.Length);
        _destination.Write(buffer);
        _bytesWritten += buffer.Length;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureCapacity(buffer.Length);
        await _destination.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _bytesWritten += buffer.Length;
    }
#endif

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    private void EnsureCapacity(int count) {
        long attempted = checked(_bytesWritten + count);
        if (attempted > _maximumBytes) {
            throw new EmailLimitExceededException(
                nameof(EmailReaderOptions.MaxInputBytes), attempted, _maximumBytes);
        }
    }
}
