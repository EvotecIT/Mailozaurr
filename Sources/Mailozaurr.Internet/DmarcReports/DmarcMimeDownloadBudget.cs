namespace Mailozaurr.DmarcReports;

using MailKit;

internal readonly struct BoundedMimeMessage {
    internal BoundedMimeMessage(MimeMessage message, long byteCount) {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        ByteCount = byteCount;
    }

    internal MimeMessage Message { get; }
    internal long ByteCount { get; }
}

internal sealed class DmarcMimeDownloadBudget {
    private readonly object _reservationGate = new();
    private readonly SemaphoreSlim _reservationChanged = new(0, int.MaxValue);
    private readonly long _maxBytesPerMessage;
    private long _remainingBytes;
    private int _activeReservations;
    private int _waitingReservations;

    internal DmarcMimeDownloadBudget(long maxBytesPerMessage, long maxTotalBytes) {
        _maxBytesPerMessage = maxBytesPerMessage;
        _remainingBytes = maxTotalBytes;
    }

    internal ITransferProgress CreateTransferProgress() =>
        new BoundedTransferProgress(this, _maxBytesPerMessage);

    internal async Task<MimeMessage> DownloadAsync(
        Func<long, CancellationToken, Task<BoundedMimeMessage>> download,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        long limit = await ReserveAllowanceAsync(cancellationToken).ConfigureAwait(false);
        long refund = 0;
        // Reserve the complete per-message allowance before any provider I/O. Successful
        // downloads refund unused bytes, while malformed, truncated, or canceled transfers
        // retain the reservation so repeated failures cannot bypass the aggregate limit.
        try {
            BoundedMimeMessage result = await download(limit, cancellationToken).ConfigureAwait(false);
            if (result.ByteCount < 0 || result.ByteCount > limit) {
                throw new InvalidDataException("DMARC MIME message exceeded the configured byte limit.");
            }
            refund = limit - result.ByteCount;
            return result.Message;
        } finally {
            CompleteReservation(refund);
        }
    }

    private async Task<long> ReserveAllowanceAsync(CancellationToken cancellationToken) {
        bool registeredWaiter = false;
        while (true) {
            lock (_reservationGate) {
                if (registeredWaiter) {
                    _waitingReservations--;
                    registeredWaiter = false;
                }
                long limit = Math.Min(_maxBytesPerMessage, _remainingBytes);
                if (limit > 0) {
                    _remainingBytes -= limit;
                    _activeReservations++;
                    return limit;
                }
                if (_activeReservations == 0) {
                    throw new InvalidDataException("DMARC MIME downloads exceeded the configured total byte limit.");
                }
                _waitingReservations++;
                registeredWaiter = true;
            }
            try {
                await _reservationChanged.WaitAsync(cancellationToken).ConfigureAwait(false);
            } catch {
                lock (_reservationGate) {
                    if (registeredWaiter) _waitingReservations--;
                }
                throw;
            }
        }
    }

    private void CompleteReservation(long refund) {
        int waiting;
        lock (_reservationGate) {
            _remainingBytes += refund;
            _activeReservations--;
            waiting = _waitingReservations;
        }
        if (waiting > 0) _reservationChanged.Release(waiting);
    }

    private void Consume(long count) {
        if (count <= 0) return;
        while (true) {
            long remaining = Interlocked.Read(ref _remainingBytes);
            if (count > remaining) {
                throw new InvalidDataException("DMARC MIME downloads exceeded the configured total byte limit.");
            }
            if (Interlocked.CompareExchange(ref _remainingBytes, remaining - count, remaining) == remaining) return;
        }
    }

    private sealed class BoundedTransferProgress : ITransferProgress {
        private readonly DmarcMimeDownloadBudget _owner;
        private readonly long _maximumBytes;
        private long _reportedBytes;

        internal BoundedTransferProgress(DmarcMimeDownloadBudget owner, long maximumBytes) {
            _owner = owner;
            _maximumBytes = maximumBytes;
        }

        public void Report(long bytesTransferred) => Report(bytesTransferred, -1);

        public void Report(long bytesTransferred, long totalSize) {
            if (bytesTransferred < _reportedBytes) return;
            if (bytesTransferred > _maximumBytes || totalSize > _maximumBytes) {
                throw new InvalidDataException("DMARC MIME message exceeded the configured byte limit.");
            }
            long delta = bytesTransferred - _reportedBytes;
            _owner.Consume(delta);
            _reportedBytes = bytesTransferred;
        }
    }
}
