using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Listens for new mail using the IMAP IDLE command and raises events when messages arrive.
/// </summary>
/// <remarks>
/// An internal cache of <see cref="IMessageSummary"/> objects is maintained
/// to ensure each message is reported only once per session.
/// </remarks>
public class ImapIdleListener : IDisposable, IAsyncDisposable {
    private readonly ImapClient _client;
    private readonly string? _folderName;
    private readonly List<UniqueId> _summaries = new();
    private readonly HashSet<UniqueId> _known = new();
    private readonly FetchRequest _fetchRequest = new(MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope);
    private readonly SearchQuery? _searchQuery;
    private readonly bool _downloadMessageContent;
    private IMailFolder? _folder;
    private CancellationTokenSource? _cancel;
    private CancellationTokenSource? _done;
    private bool _messagesArrived;
    private Task? _idleTask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImapIdleListener"/> class.
    /// </summary>
    /// <param name="client">Connected IMAP client.</param>
    /// <param name="folder">Folder to monitor or <c>null</c> for the inbox.</param>
    /// <param name="searchQuery">Optional search query to filter incoming messages.</param>
    public ImapIdleListener(ImapClient client, string? folder = null, SearchQuery? searchQuery = null)
        : this(client, folder, searchQuery, downloadMessageContent: true) {
    }

    /// <summary>
    /// Initializes a listener with explicit control over full-message downloads.
    /// </summary>
    public ImapIdleListener(
        ImapClient client,
        string? folder,
        SearchQuery? searchQuery,
        bool downloadMessageContent) {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _folderName = folder;
        _searchQuery = searchQuery;
        _downloadMessageContent = downloadMessageContent;
    }

    /// <summary>
    /// Occurs when a new message arrives.
    /// </summary>
    public event EventHandler<ImapEmailMessage>? MessageArrived;

    /// <summary>
    /// Occurs when a new message summary arrives, without requiring a MIME body download.
    /// </summary>
    public event EventHandler<ImapMessageSummaryEventArgs>? MessageSummaryArrived;

    /// <summary>
    /// Occurs when an error is encountered while idling.
    /// </summary>
    public event EventHandler<Exception>? IdleError;

    /// <summary>
    /// Starts listening for new messages.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default) {
        if (_disposed) {
            throw new ObjectDisposedException(nameof(ImapIdleListener));
        }

        if (_cancel != null) {
            throw new InvalidOperationException("Listener already started.");
        }

        _cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try {
            _folder = _client.GetCachedFolder(_folderName, FolderAccess.ReadOnly);
            await _folder.OpenAsync(FolderAccess.ReadOnly, _cancel.Token).ConfigureAwait(false);

            var search = _searchQuery ?? SearchQuery.All;
            var initialUids = await _folder.SearchAsync(search, _cancel.Token).ConfigureAwait(false);
            foreach (var uid in initialUids) {
                _summaries.Add(uid);
                _known.Add(uid);
            }

            // Fix the baseline before subscribing, then reconcile once after the
            // handlers are attached. This closes both setup windows: an arrival
            // between the baseline search and subscription is found by the
            // reconciliation search, while an arrival during reconciliation sets
            // _messagesArrived and is checked again by the idle loop.
            _folder.CountChanged += OnCountChanged;
            _folder.MessageExpunged += OnMessageExpunged;
            await FetchNewMessagesAsync().ConfigureAwait(false);

            _idleTask = IdleLoopAsync();
        } catch {
            if (_folder?.IsOpen == true) {
                try {
                    await _folder.CloseAsync(expunge: false, CancellationToken.None).ConfigureAwait(false);
                } catch {
                }
            }

            _summaries.Clear();
            _known.Clear();
            Cleanup();
            _idleTask = null;
            throw;
        }
    }

    /// <summary>
    /// Stops listening for new messages.
    /// </summary>
    public void Stop() => _cancel?.Cancel();

    /// <summary>
    /// Stops listening for new messages and waits for the listener loop to finish.
    /// </summary>
    public async Task StopAsync() {
        var idleTask = _idleTask;
        var cancel = _cancel;

        Stop();

        if (idleTask == null) {
            Cleanup();
            return;
        }

        try {
            await idleTask.ConfigureAwait(false);
        } catch (OperationCanceledException) when (cancel?.IsCancellationRequested == true) {
            // Listener shutdown requested cancellation while the loop was unwinding.
        } finally {
            Cleanup();
            if (ReferenceEquals(_idleTask, idleTask)) {
                _idleTask = null;
            }
        }
    }

    private async Task IdleLoopAsync() {
        try {
            while (!_cancel!.IsCancellationRequested) {
                try {
                    if (_messagesArrived) {
                        _messagesArrived = false;
                        await FetchNewMessagesAsync().ConfigureAwait(false);
                        continue;
                    }

                    await WaitForNewMessagesAsync().ConfigureAwait(false);
                } catch (OperationCanceledException) when (_cancel.IsCancellationRequested) {
                    break;
                } catch (Exception ex) {
                    IdleError?.Invoke(this, ex);
                    try {
                        await Task.Delay(TimeSpan.FromSeconds(5), _cancel.Token).ConfigureAwait(false);
                    } catch (OperationCanceledException) when (_cancel.IsCancellationRequested) {
                        break;
                    }
                }
            }
        } finally {
            Cleanup();
        }
    }

    private async Task WaitForNewMessagesAsync() {
        if (_client.Capabilities.HasFlag(ImapCapabilities.Idle)) {
            _done = new CancellationTokenSource(TimeSpan.FromMinutes(9));
            try {
                await _client.IdleAsync(_done.Token, _cancel!.Token).ConfigureAwait(false);
            } finally {
                _done.Dispose();
                _done = null;
            }
        } else {
            await Task.Delay(TimeSpan.FromMinutes(1), _cancel!.Token).ConfigureAwait(false);
            await _client.NoOpAsync(_cancel.Token).ConfigureAwait(false);
        }
    }

    private async Task FetchNewMessagesAsync() {
        var search = _searchQuery ?? SearchQuery.All;
        var uids = await _folder!.SearchAsync(search, _cancel!.Token).ConfigureAwait(false);
        var newUids = new List<UniqueId>();
        foreach (var uid in uids) {
            if (!_known.Contains(uid)) newUids.Add(uid);
        }

        if (newUids.Count == 0) return;

        var fetched = await _folder.FetchAsync(newUids, _fetchRequest, _cancel.Token).ConfigureAwait(false);
        foreach (var summary in fetched) {
            if (!_known.Add(summary.UniqueId)) continue;
            _summaries.Add(summary.UniqueId);
            MessageSummaryArrived?.Invoke(this, new ImapMessageSummaryEventArgs(
                summary.UniqueId,
                summary.Envelope?.Subject));
            if (_downloadMessageContent && MessageArrived != null) {
                var message = await _folder.GetMessageAsync(summary.UniqueId, _cancel.Token).ConfigureAwait(false);
                MessageArrived.Invoke(this, new ImapEmailMessage(summary.UniqueId, message));
            }
        }
    }

    private void OnCountChanged(object? sender, EventArgs e) {
        _messagesArrived = true;
        _done?.Cancel();
    }

    private void OnMessageExpunged(object? sender, MessageEventArgs e) {
        if (e.UniqueId.HasValue) {
            _known.Remove(e.UniqueId.Value);
            _summaries.RemoveAll(uid => uid == e.UniqueId.Value);
        } else if (e.Index < _summaries.Count) {
            var removed = _summaries[e.Index];
            _summaries.RemoveAt(e.Index);
            _known.Remove(removed);
        }
    }

    /// <inheritdoc />
    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;

        try {
            Stop();

            var idleTask = _idleTask;
            if (idleTask != null) {
                try {
                    idleTask.GetAwaiter().GetResult();
                } catch (OperationCanceledException) when (_cancel?.IsCancellationRequested == true) {
                    // Listener shutdown requested cancellation while the loop was unwinding.
                }
            }
        } finally {
            Cleanup();
            _idleTask = null;
            GC.SuppressFinalize(this);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;

        try {
            await StopAsync().ConfigureAwait(false);
        } finally {
            _idleTask = null;
            GC.SuppressFinalize(this);
        }
    }

    private void Cleanup() {
        if (_folder != null) {
            _folder.CountChanged -= OnCountChanged;
            _folder.MessageExpunged -= OnMessageExpunged;
            _folder = null;
        }

        _done?.Dispose();
        _done = null;

        _cancel?.Dispose();
        _cancel = null;

        _messagesArrived = false;
    }
}
