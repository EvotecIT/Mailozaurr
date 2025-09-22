using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Polls Microsoft Graph for new messages and raises events when they arrive.
/// </summary>
/// <remarks>
/// The listener keeps track of message IDs to avoid raising duplicate
/// notifications during polling.
/// </remarks>
public class GraphMessageListener : IDisposable, IAsyncDisposable {
    private readonly GraphCredential _credential;
    private readonly string _userPrincipalName;
    private readonly HashSet<string> _seenIds = new();
    private CancellationTokenSource? _cancel;
    private Task? _pollTask;
    private readonly TimeSpan _interval;
    private Task? _disposeTask;
    private int _disposeState;

    private const int DisposeStateActive = 0;
    private const int DisposeStateDisposing = 1;
    private const int DisposeStateDisposed = 2;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphMessageListener"/> class.
    /// </summary>
    /// <param name="credential">Graph credential to use.</param>
    /// <param name="userPrincipalName">User principal name to monitor.</param>
    /// <param name="interval">Polling interval.</param>
    public GraphMessageListener(GraphCredential credential, string userPrincipalName, TimeSpan? interval = null) {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _userPrincipalName = userPrincipalName ?? throw new ArgumentNullException(nameof(userPrincipalName));
        _interval = interval ?? TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Occurs when a new message arrives.
    /// </summary>
    public event EventHandler<Dictionary<string, object>>? MessageArrived;

    /// <summary>
    /// Occurs when an error is encountered while polling.
    /// </summary>
    public event EventHandler<Exception>? PollError;

    /// <summary>
    /// Starts listening for new messages.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        if (_cancel != null) {
            throw new InvalidOperationException("Listener already started.");
        }

        _cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var initial = await MicrosoftGraphUtils.GetMailMessagesAsync(_credential, _userPrincipalName).ConfigureAwait(false);
        foreach (var msg in initial) {
            if (msg.TryGetValue("id", out var idObj) && idObj is string id) {
                _seenIds.Add(id);
            }
        }

        _pollTask = PollLoopAsync();
    }

    /// <summary>
    /// Stops listening for new messages.
    /// </summary>
    public void Stop() {
        StopAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Stops listening for new messages.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel waiting for the polling loop to complete.</param>
    public async Task StopAsync(CancellationToken cancellationToken) {
        var disposeTask = Volatile.Read(ref _disposeTask);
        if (disposeTask != null) {
            await WaitWithCancellationAsync(disposeTask, cancellationToken).ConfigureAwait(false);
            ThrowIfDisposed();
            return;
        }

        ThrowIfDisposed();

        await StopAsyncCore(cancellationToken).ConfigureAwait(false);
    }

    private async Task PollLoopAsync() {
        while (!_cancel!.IsCancellationRequested) {
            try {
                await Task.Delay(_interval, _cancel.Token).ConfigureAwait(false);
                var messages = await MicrosoftGraphUtils.GetMailMessagesAsync(_credential, _userPrincipalName).ConfigureAwait(false);
                foreach (var msg in messages) {
                    if (msg.TryGetValue("id", out var idObj) && idObj is string id && !_seenIds.Contains(id)) {
                        _seenIds.Add(id);
                        MessageArrived?.Invoke(this, msg);
                    }
                }
            } catch (OperationCanceledException) when (_cancel.IsCancellationRequested) {
                break;
            } catch (Exception ex) {
                PollError?.Invoke(this, ex);
                await Task.Delay(TimeSpan.FromSeconds(5), _cancel.Token).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => new ValueTask(EnsureDisposeTask());

    private Task EnsureDisposeTask() {
        while (true) {
            var existing = Volatile.Read(ref _disposeTask);
            if (existing != null) {
                return existing;
            }

            var created = DisposeAsyncCore();
            if (Interlocked.CompareExchange(ref _disposeTask, created, null) == null) {
                return created;
            }
        }
    }

    private async Task DisposeAsyncCore() {
        if (Interlocked.CompareExchange(ref _disposeState, DisposeStateDisposing, DisposeStateActive) != DisposeStateActive) {
            return;
        }

        try {
            await StopAsyncCore(CancellationToken.None).ConfigureAwait(false);
        } finally {
            Volatile.Write(ref _disposeState, DisposeStateDisposed);
            GC.SuppressFinalize(this);
        }
    }

    private async Task StopAsyncCore(CancellationToken cancellationToken) {
        var cancel = _cancel;
        if (cancel == null) {
            return;
        }

        Task? pollTask = _pollTask;
        cancel.Cancel();

        try {
            if (pollTask != null) {
                try {
                    await WaitWithCancellationAsync(pollTask, cancellationToken).ConfigureAwait(false);
                } catch (OperationCanceledException) when (cancel.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
                    // Expected cancellation from the listener itself.
                }
            }
        } finally {
            cancel.Dispose();
            _cancel = null;
            _pollTask = null;
        }
    }

    private void ThrowIfDisposed() {
        if (Volatile.Read(ref _disposeState) == DisposeStateDisposed) {
            throw new ObjectDisposedException(nameof(GraphMessageListener));
        }
    }

    private static async Task WaitWithCancellationAsync(Task task, CancellationToken cancellationToken) {
        if (!cancellationToken.CanBeCanceled) {
            await task.ConfigureAwait(false);
            return;
        }

        if (task.IsCompleted) {
            await task.ConfigureAwait(false);
            return;
        }

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(static state => ((TaskCompletionSource<object?>)state!).TrySetResult(null), tcs)) {
            if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(false)) {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        await task.ConfigureAwait(false);
    }
}
