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
public class Pop3PollListener : IDisposable {
    private readonly Pop3Client _client;
    private int _seenCount;
    private CancellationTokenSource? _cancel;
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
    public Task StartAsync(CancellationToken cancellationToken = default) {
        if (_cancel != null) {
            throw new InvalidOperationException("Listener already started.");
        }

        _cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _seenCount = _client.Count;
        _ = PollLoopAsync();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops listening for new messages.
    /// </summary>
    public void Stop() => _cancel?.Cancel();

    private async Task PollLoopAsync() {
        while (!_cancel!.IsCancellationRequested) {
            try {
                await Task.Delay(_interval, _cancel.Token).ConfigureAwait(false);
                await _client.NoOpAsync(_cancel.Token).ConfigureAwait(false);
                var count = _client.Count;
                if (count > _seenCount) {
                    for (var i = _seenCount; i < count; i++) {
                        var message = await _client.GetMessageAsync(i, _cancel.Token).ConfigureAwait(false);
                        MessageArrived?.Invoke(this, new Pop3EmailMessage(i, message));
                    }
                    _seenCount = count;
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
        Stop();
    }
}
