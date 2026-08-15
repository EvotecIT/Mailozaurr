using OfficeIMO.Email;

namespace Mailozaurr;

internal static class MailFileMimeTemporaryStorage {
    internal static FileStream Create(bool asynchronous) {
        string path = Path.Combine(Path.GetTempPath(),
            string.Concat("mailozaurr-mime-", Guid.NewGuid().ToString("N"), ".tmp"));
        FileOptions options = FileOptions.DeleteOnClose | FileOptions.SequentialScan;
        if (asynchronous) options |= FileOptions.Asynchronous;
#if NET8_0_OR_GREATER
        var streamOptions = new FileStreamOptions {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            BufferSize = 81920,
            Options = options
        };
        if (!OperatingSystem.IsWindows()) {
            streamOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }
        return new FileStream(path, streamOptions);
#else
        return new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite,
            FileShare.None, 81920, options);
#endif
    }
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
