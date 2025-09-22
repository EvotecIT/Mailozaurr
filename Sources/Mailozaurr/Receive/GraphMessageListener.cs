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
public class GraphMessageListener : IDisposable {
    private readonly GraphCredential _credential;
    private readonly string _userPrincipalName;
    private readonly Dictionary<string, DateTimeOffset> _seenIds = new();
    private readonly Queue<(string Id, DateTimeOffset Timestamp)> _seenQueue = new();
    private readonly object _seenLock = new();
    private CancellationTokenSource? _cancel;
    private Task? _pollTask;
    private readonly TimeSpan _interval;
    private readonly GraphMessageListenerRetentionOptions _retentionOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphMessageListener"/> class.
    /// </summary>
    /// <param name="credential">Graph credential to use.</param>
    /// <param name="userPrincipalName">User principal name to monitor.</param>
    /// <param name="interval">Polling interval.</param>
    /// <param name="retentionOptions">Policy controlling how seen message identifiers are retained.</param>
    public GraphMessageListener(
        GraphCredential credential,
        string userPrincipalName,
        TimeSpan? interval = null,
        GraphMessageListenerRetentionOptions? retentionOptions = null) {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _userPrincipalName = userPrincipalName ?? throw new ArgumentNullException(nameof(userPrincipalName));
        _interval = interval ?? TimeSpan.FromMinutes(1);
        _retentionOptions = (retentionOptions ?? new GraphMessageListenerRetentionOptions()).Clone();
        if (!_retentionOptions.MaxSeenIds.HasValue && !_retentionOptions.SlidingExpiration.HasValue) {
            throw new ArgumentException(
                "Retention options must configure either MaxSeenIds or SlidingExpiration.",
                nameof(retentionOptions));
        }
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
        if (_cancel != null) {
            throw new InvalidOperationException("Listener already started.");
        }

        _cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var initial = await MicrosoftGraphUtils.GetMailMessagesAsync(_credential, _userPrincipalName).ConfigureAwait(false);
        foreach (var msg in initial) {
            if (msg.TryGetValue("id", out var idObj) && idObj is string id) {
                TrackSeenMessage(id);
            }
        }

        _pollTask = PollLoopAsync();
    }

    /// <summary>
    /// Stops listening for new messages.
    /// </summary>
    public void Stop() {
        if (_cancel == null) {
            return;
        }

        _cancel.Cancel();
        try {
            _pollTask?.GetAwaiter().GetResult();
        } catch (OperationCanceledException) {
            // ignored
        }

        _cancel.Dispose();
        _cancel = null;
        _pollTask = null;
    }

    private async Task PollLoopAsync() {
        while (!_cancel!.IsCancellationRequested) {
            try {
                await Task.Delay(_interval, _cancel.Token).ConfigureAwait(false);
                var messages = await MicrosoftGraphUtils.GetMailMessagesAsync(_credential, _userPrincipalName).ConfigureAwait(false);
                foreach (var msg in messages) {
                    if (msg.TryGetValue("id", out var idObj) && idObj is string id && TryRegisterMessage(id)) {
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

    private bool TryRegisterMessage(string id) {
        var now = _retentionOptions.Clock();
        lock (_seenLock) {
            PruneExpiredEntries(now);
            var alreadySeen = RecordSeenMessage(id, now);
            TrimCapacity();
            return !alreadySeen;
        }
    }

    private void TrackSeenMessage(string id) {
        var now = _retentionOptions.Clock();
        lock (_seenLock) {
            PruneExpiredEntries(now);
            RecordSeenMessage(id, now);
            TrimCapacity();
        }
    }

    private bool RecordSeenMessage(string id, DateTimeOffset timestamp) {
        var alreadySeen = _seenIds.TryGetValue(id, out _);
        _seenIds[id] = timestamp;
        _seenQueue.Enqueue((id, timestamp));
        return alreadySeen;
    }

    private void PruneExpiredEntries(DateTimeOffset now) {
        if (!_retentionOptions.SlidingExpiration.HasValue) {
            return;
        }

        var expirationThreshold = now - _retentionOptions.SlidingExpiration.Value;
        while (_seenQueue.Count > 0) {
            var (seenId, timestamp) = _seenQueue.Peek();
            if (!_seenIds.TryGetValue(seenId, out var recordedTimestamp) || recordedTimestamp > timestamp) {
                _seenQueue.Dequeue();
                continue;
            }

            if (timestamp < expirationThreshold) {
                _seenQueue.Dequeue();
                if (recordedTimestamp <= timestamp) {
                    _seenIds.Remove(seenId);
                }
            } else {
                break;
            }
        }
    }

    private void TrimCapacity() {
        if (!_retentionOptions.MaxSeenIds.HasValue) {
            return;
        }

        while (_seenIds.Count > _retentionOptions.MaxSeenIds.Value && _seenQueue.Count > 0) {
            var (seenId, timestamp) = _seenQueue.Dequeue();
            if (_seenIds.TryGetValue(seenId, out var recordedTimestamp) && recordedTimestamp <= timestamp) {
                _seenIds.Remove(seenId);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() {
        Stop();
        _cancel?.Dispose();
        _cancel = null;
    }
}
