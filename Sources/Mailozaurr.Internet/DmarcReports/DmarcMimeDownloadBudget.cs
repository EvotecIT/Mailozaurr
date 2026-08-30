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
    private readonly LinkedList<ReservationWaiter> _reservationWaiters = new();
    private readonly long _maxBytesPerMessage;
    private long _remainingBytes;
    private int _activeReservations;

    internal DmarcMimeDownloadBudget(long maxBytesPerMessage, long maxTotalBytes) {
        _maxBytesPerMessage = maxBytesPerMessage;
        _remainingBytes = maxTotalBytes;
    }

    // MailKit exposes actual byte progress rather than a provider download callback that
    // accepts an allowance. Consume those bytes atomically; Graph and Gmail use DownloadAsync
    // so their independently started provider requests reserve and refund explicit allowances.
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
        cancellationToken.ThrowIfCancellationRequested();
        var waiter = new ReservationWaiter();
        lock (_reservationGate) {
            waiter.Node = _reservationWaiters.AddLast(waiter);
            GrantWaitingReservationsLocked();
        }
        using CancellationTokenRegistration registration = cancellationToken.Register(
            () => CancelReservation(waiter));
        return await waiter.Completion.Task.ConfigureAwait(false);
    }

    private void CompleteReservation(long refund) {
        lock (_reservationGate) {
            _remainingBytes += refund;
            _activeReservations--;
            GrantWaitingReservationsLocked();
        }
    }

    private void CancelReservation(ReservationWaiter waiter) {
        lock (_reservationGate) {
            if (waiter.Node?.List == null) return;
            _reservationWaiters.Remove(waiter.Node);
            waiter.Node = null;
            waiter.Completion.TrySetCanceled();
            GrantWaitingReservationsLocked();
        }
    }

    private void GrantWaitingReservationsLocked() {
        while (_reservationWaiters.First != null) {
            bool canReserveFullAllowance = _remainingBytes >= _maxBytesPerMessage;
            bool canReserveFinalPartialAllowance = _remainingBytes > 0 && _activeReservations == 0;
            if (!canReserveFullAllowance && !canReserveFinalPartialAllowance) {
                if (_activeReservations != 0) return;
                while (_reservationWaiters.First != null) {
                    ReservationWaiter rejected = _reservationWaiters.First.Value;
                    _reservationWaiters.RemoveFirst();
                    rejected.Node = null;
                    rejected.Completion.TrySetException(new InvalidDataException(
                        "DMARC MIME downloads exceeded the configured total byte limit."));
                }
                return;
            }

            ReservationWaiter granted = _reservationWaiters.First.Value;
            _reservationWaiters.RemoveFirst();
            granted.Node = null;
            long limit = Math.Min(_maxBytesPerMessage, _remainingBytes);
            _remainingBytes -= limit;
            _activeReservations++;
            granted.Completion.TrySetResult(limit);
        }
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

    private sealed class ReservationWaiter {
        internal TaskCompletionSource<long> Completion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        internal LinkedListNode<ReservationWaiter>? Node { get; set; }
    }
}
