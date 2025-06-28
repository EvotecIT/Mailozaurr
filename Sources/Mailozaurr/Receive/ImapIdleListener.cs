using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Listens for new mail using the IMAP IDLE command and raises events when messages arrive.
/// </summary>
public class ImapIdleListener : IDisposable {
    private readonly ImapClient _client;
    private readonly string? _folderName;
    private readonly List<IMessageSummary> _summaries = new();
    private readonly FetchRequest _fetchRequest = new(MessageSummaryItems.Full | MessageSummaryItems.UniqueId);
    private IMailFolder? _folder;
    private CancellationTokenSource? _cancel;
    private CancellationTokenSource? _done;
    private bool _messagesArrived;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImapIdleListener"/> class.
    /// </summary>
    /// <param name="client">Connected IMAP client.</param>
    /// <param name="folder">Folder to monitor or <c>null</c> for the inbox.</param>
    public ImapIdleListener(ImapClient client, string? folder = null) {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _folderName = folder;
    }

    /// <summary>
    /// Occurs when a new message arrives.
    /// </summary>
    public event EventHandler<ImapEmailMessage>? MessageArrived;

    /// <summary>
    /// Occurs when an error is encountered while idling.
    /// </summary>
    public event EventHandler<Exception>? IdleError;

    /// <summary>
    /// Starts listening for new messages.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default) {
        if (_cancel != null) {
            throw new InvalidOperationException("Listener already started.");
        }

        _cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _folder = _client.GetCachedFolder(_folderName, FolderAccess.ReadOnly);
        await _folder.OpenAsync(FolderAccess.ReadOnly, _cancel.Token).ConfigureAwait(false);

        var initial = await _folder.FetchAsync(0, -1, _fetchRequest, _cancel.Token).ConfigureAwait(false);
        _summaries.AddRange(initial);

        _folder.CountChanged += OnCountChanged;
        _folder.MessageExpunged += OnMessageExpunged;

        _ = IdleLoopAsync();
    }

    /// <summary>
    /// Stops listening for new messages.
    /// </summary>
    public void Stop() => _cancel?.Cancel();

    private async Task IdleLoopAsync() {
        while (!_cancel!.IsCancellationRequested) {
            try {
                await WaitForNewMessagesAsync().ConfigureAwait(false);

                if (_messagesArrived) {
                    await FetchNewMessagesAsync().ConfigureAwait(false);
                    _messagesArrived = false;
                }
            } catch (OperationCanceledException) when (_cancel.IsCancellationRequested) {
                break;
            } catch (Exception ex) {
                IdleError?.Invoke(this, ex);
                await Task.Delay(TimeSpan.FromSeconds(5), _cancel.Token).ConfigureAwait(false);
            }
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
        var fetched = await _folder!.FetchAsync(_summaries.Count, -1, _fetchRequest, _cancel!.Token).ConfigureAwait(false);
        foreach (var summary in fetched) {
            var message = await _folder.GetMessageAsync(summary.UniqueId, _cancel.Token).ConfigureAwait(false);
            _summaries.Add(summary);
            MessageArrived?.Invoke(this, new ImapEmailMessage(summary.UniqueId, message));
        }
    }

    private void OnCountChanged(object? sender, EventArgs e) {
        if (_folder!.Count > _summaries.Count) {
            _messagesArrived = true;
            _done?.Cancel();
        }
    }

    private void OnMessageExpunged(object? sender, MessageEventArgs e) {
        if (e.Index < _summaries.Count) {
            _summaries.RemoveAt(e.Index);
        }
    }

    /// <inheritdoc />
    public void Dispose() {
        Stop();
        if (_folder != null) {
            _folder.CountChanged -= OnCountChanged;
            _folder.MessageExpunged -= OnMessageExpunged;
        }
    }
}
