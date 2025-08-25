namespace Mailozaurr;

using System;
using System.IO;

/// <summary>
/// Stream wrapper that limits the number of bytes that can be read.
/// </summary>
internal sealed class LimitedStream : Stream {
    private readonly Stream _inner;
    private readonly long _maxBytes;
    private long _totalBytes;

    public LimitedStream(Stream inner, long maxBytes) {
        _inner = inner;
        _maxBytes = maxBytes;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;
    public override long Position {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) {
        var remaining = _maxBytes - _totalBytes;
        if (remaining <= 0) throw new InvalidDataException("Uncompressed data exceeds allowed limit.");
        if (count > remaining) count = (int)remaining;
        var read = _inner.Read(buffer, offset, count);
        _totalBytes += read;
        if (_totalBytes > _maxBytes) throw new InvalidDataException("Uncompressed data exceeds allowed limit.");
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
