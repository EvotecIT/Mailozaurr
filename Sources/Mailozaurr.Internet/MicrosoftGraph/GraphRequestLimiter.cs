namespace Mailozaurr;

/// <summary>Maintains one process-wide Graph admission queue while its limit changes.</summary>
internal sealed class GraphRequestLimiter {
    private readonly object gate = new();
    private readonly Queue<TaskCompletionSource<bool>> waiters = new();
    private int active;
    private int limit;

    internal GraphRequestLimiter(int limit) => this.limit = limit;

    internal int Limit {
        get { lock (gate) return limit; }
        set {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            lock (gate) {
                limit = value;
                AdmitWaiters();
            }
        }
    }

    internal Task WaitAsync(CancellationToken cancellationToken) {
        lock (gate) {
            cancellationToken.ThrowIfCancellationRequested();
            if (active < limit && waiters.Count == 0) {
                active++;
                return Task.CompletedTask;
            }
            var waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            waiters.Enqueue(waiter);
            return WaitForAdmissionAsync(waiter, cancellationToken);
        }
    }

    private async Task WaitForAdmissionAsync(TaskCompletionSource<bool> waiter, CancellationToken cancellationToken) {
        using var registration = cancellationToken.Register(() => {
            lock (gate) {
                waiter.TrySetCanceled(cancellationToken);
                AdmitWaiters();
            }
        });
        await waiter.Task.ConfigureAwait(false);
    }

    internal void Release() {
        lock (gate) {
            if (active == 0) throw new SemaphoreFullException();
            active--;
            AdmitWaiters();
        }
    }

    private void AdmitWaiters() {
        while (waiters.Count > 0) {
            var waiter = waiters.Peek();
            if (waiter.Task.IsCanceled) {
                waiters.Dequeue();
                continue;
            }
            if (active >= limit) break;
            waiters.Dequeue();
            if (waiter.TrySetResult(true)) active++;
        }
    }
}
