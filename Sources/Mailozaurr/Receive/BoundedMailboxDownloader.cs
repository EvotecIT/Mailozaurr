namespace Mailozaurr;

/// <summary>Coordinates ordered mailbox downloads without exceeding the requested concurrency.</summary>
internal static class BoundedMailboxDownloader {
    internal static async Task<IReadOnlyList<TResult>> DownloadInOrderAsync<TItem, TResult>(
        IReadOnlyList<TItem> items,
        int parallelDownloadLimit,
        Func<TItem, CancellationToken, Task<TResult>> download,
        CancellationToken cancellationToken) {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (download == null) throw new ArgumentNullException(nameof(download));
        if (items.Count == 0) return Array.Empty<TResult>();

        int workerCount = Math.Min(Math.Max(1, parallelDownloadLimit), items.Count);
        var results = new TResult[items.Count];
        int nextIndex = -1;

        async Task WorkerAsync() {
            while (true) {
                cancellationToken.ThrowIfCancellationRequested();
                int index = Interlocked.Increment(ref nextIndex);
                if (index >= items.Count) return;
                results[index] = await download(items[index], cancellationToken).ConfigureAwait(false);
            }
        }

        var workers = new Task[workerCount];
        for (int index = 0; index < workers.Length; index++) workers[index] = WorkerAsync();
        await Task.WhenAll(workers).ConfigureAwait(false);
        return results;
    }
}
