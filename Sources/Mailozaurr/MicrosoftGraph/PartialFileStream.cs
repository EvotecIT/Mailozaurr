using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

internal sealed class PartialFileStream : Stream
{
    private readonly FileStream _stream;
    private readonly long _length;
    private long _remaining;

    public PartialFileStream(string path, long offset, long length)
    {
        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        _stream.Seek(offset, SeekOrigin.Begin);
        _length = length;
        _remaining = length;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position
    {
        get => _length - _remaining;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_remaining <= 0)
        {
            return 0;
        }

        if (count > _remaining)
        {
            count = (int)_remaining;
        }

        int read = _stream.Read(buffer, offset, count);
        _remaining -= read;
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_remaining <= 0)
        {
            return 0;
        }

        if (count > _remaining)
        {
            count = (int)_remaining;
        }

        int read = await _stream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        _remaining -= read;
        return read;
    }

    public override void Flush() => _stream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream.Dispose();
        }
        base.Dispose(disposing);
    }
}
