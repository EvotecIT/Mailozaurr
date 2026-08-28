namespace Mailozaurr;

using System;
using System.IO;
using System.Threading;

/// <summary>Thread-safe byte budget shared by bounded stream readers.</summary>
internal sealed class SharedReadBudget {
    private long _remaining;

    internal SharedReadBudget(long maximumBytes) {
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        _remaining = maximumBytes;
    }

    internal long Remaining => Interlocked.Read(ref _remaining);

    internal int Reserve(int requested) {
        while (true) {
            long remaining = Interlocked.Read(ref _remaining);
            if (remaining <= 0) return 0;
            int reserved = (int)Math.Min(remaining, requested);
            if (Interlocked.CompareExchange(ref _remaining, remaining - reserved, remaining) == remaining) {
                return reserved;
            }
        }
    }

    internal void Refund(int count) {
        if (count > 0) Interlocked.Add(ref _remaining, count);
    }
}

/// <summary>Read-only stream that consumes both local and operation-wide byte budgets.</summary>
internal sealed class SharedBudgetReadStream : Stream {
    private readonly Stream _inner;
    private readonly SharedReadBudget _localBudget;
    private readonly SharedReadBudget _operationBudget;
    private readonly CancellationToken _cancellationToken;

    internal SharedBudgetReadStream(
        Stream inner,
        SharedReadBudget localBudget,
        SharedReadBudget operationBudget,
        CancellationToken cancellationToken = default) {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _localBudget = localBudget ?? throw new ArgumentNullException(nameof(localBudget));
        _operationBudget = operationBudget ?? throw new ArgumentNullException(nameof(operationBudget));
        _cancellationToken = cancellationToken;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count) {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (buffer.Length - offset < count) throw new ArgumentException("The offset and count exceed the buffer length.");
        if (count == 0) return 0;
        _cancellationToken.ThrowIfCancellationRequested();

        int localReservation = _localBudget.Reserve(count);
        if (localReservation == 0) return ProbeForLimitViolation();

        int operationReservation = _operationBudget.Reserve(localReservation);
        if (operationReservation == 0) {
            _localBudget.Refund(localReservation);
            return ProbeForLimitViolation();
        }
        if (operationReservation < localReservation) {
            _localBudget.Refund(localReservation - operationReservation);
        }

        int read;
        try {
            read = _inner.Read(buffer, offset, operationReservation);
        } catch {
            _localBudget.Refund(operationReservation);
            _operationBudget.Refund(operationReservation);
            throw;
        }

        int unused = operationReservation - read;
        _localBudget.Refund(unused);
        _operationBudget.Refund(unused);
        return read;
    }

    private int ProbeForLimitViolation() {
        _cancellationToken.ThrowIfCancellationRequested();
        int value = _inner.ReadByte();
        if (value < 0) return 0;
        throw new InvalidDataException("Uncompressed DMARC attachment data exceeds the configured limit.");
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
