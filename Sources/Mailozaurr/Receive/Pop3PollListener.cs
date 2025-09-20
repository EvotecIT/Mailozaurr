using MailKit.Net.Pop3;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Polls a POP3 mailbox for new messages and raises events when they arrive.
/// </summary>
/// <remarks>
/// Uses basic polling rather than the IMAP IDLE approach and is
/// therefore best suited for servers without IDLE support.
/// </remarks>
public class Pop3PollListener : IDisposable, IAsyncDisposable {
    private readonly Pop3Client _client;
    private readonly HashSet<string> _knownUids = new HashSet<string>(StringComparer.Ordinal);
    private CancellationTokenSource? _cancel;
    private Task? _pollingTask;
    private readonly TimeSpan _interval;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pop3PollListener"/> class.
    /// </summary>
    /// <param name="client">Connected POP3 client.</param>
    /// <param name="interval">Polling interval.</param>
    public Pop3PollListener(Pop3Client client, TimeSpan? interval = null) {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _interval = interval ?? TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Occurs when a new message arrives.
    /// </summary>
    public event EventHandler<Pop3EmailMessage>? MessageArrived;

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
        _knownUids.Clear();
        var count = GetMessageCount();
        for (var i = 0; i < count; i++) {
            var uid = await GetMessageUidAsync(i, _cancel.Token).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(uid)) {
                _knownUids.Add(uid);
            }
        }
        _pollingTask = PollLoopAsync();
    }

    /// <summary>
    /// Stops listening for new messages.
    /// </summary>
    public void Stop() => StopAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Stops listening for new messages and waits for the polling loop to finish.
    /// </summary>
    public async Task StopAsync() {
        var cancel = _cancel;
        var pollingTask = _pollingTask;

        if (cancel == null) {
            if (pollingTask != null) {
                try {
                    await pollingTask.ConfigureAwait(false);
                } finally {
                    _pollingTask = null;
                }
            }

            return;
        }

        try {
            cancel.Cancel();
            if (pollingTask != null) {
                try {
                    await pollingTask.ConfigureAwait(false);
                } catch (OperationCanceledException) when (cancel.IsCancellationRequested) {
                }
            }
        } finally {
            cancel.Dispose();
            _cancel = null;
            _pollingTask = null;
        }
    }

    private async Task PollLoopAsync() {
        while (!_cancel!.IsCancellationRequested) {
            try {
                await DelayAsync(_interval, _cancel.Token).ConfigureAwait(false);
                await NoOpAsync(_cancel.Token).ConfigureAwait(false);
                var count = GetMessageCount();
                var currentUids = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < count; i++) {
                    var uid = await GetMessageUidAsync(i, _cancel.Token).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(uid)) {
                        continue;
                    }

                    currentUids.Add(uid);

                    if (_knownUids.Contains(uid)) {
                        continue;
                    }

                    var message = await GetMessageAsync(i, _cancel.Token).ConfigureAwait(false);
                    MessageArrived?.Invoke(this, new Pop3EmailMessage(i, message));
                    _knownUids.Add(uid);
                }

                _knownUids.IntersectWith(currentUids);
            } catch (OperationCanceledException) when (_cancel.IsCancellationRequested) {
                break;
            } catch (Exception ex) {
                PollError?.Invoke(this, ex);
                await DelayAsync(TimeSpan.FromSeconds(5), _cancel.Token).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Retrieves the total number of messages in the current mailbox.
    /// </summary>
    /// <returns>The number of messages available.</returns>
    protected virtual int GetMessageCount() => _client.Count;

    /// <summary>
    /// Retrieves a message UID by index.
    /// </summary>
    /// <param name="index">Message index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message UID.</returns>
    protected virtual Task<string> GetMessageUidAsync(int index, CancellationToken cancellationToken) =>
        _client.GetMessageUidAsync(index, cancellationToken);

    /// <summary>
    /// Retrieves a message by index.
    /// </summary>
    /// <param name="index">Message index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message.</returns>
    protected virtual Task<MimeMessage> GetMessageAsync(int index, CancellationToken cancellationToken) =>
        _client.GetMessageAsync(index, cancellationToken);

    /// <summary>
    /// Sends a NOOP command to keep the POP3 connection alive.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected virtual Task NoOpAsync(CancellationToken cancellationToken) =>
        _client.NoOpAsync(cancellationToken);

    /// <summary>
    /// Delays the polling loop execution.
    /// </summary>
    /// <param name="interval">Delay interval.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected virtual Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken) =>
        Task.Delay(interval, cancellationToken);

    /// <inheritdoc />
    public void Dispose() {
        Stop();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() {
        await StopAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
