using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Polls Microsoft Graph for new messages and raises events when they arrive.
/// </summary>
public class GraphMessageListener : IDisposable {
    private readonly GraphCredential _credential;
    private readonly string _userPrincipalName;
    private readonly HashSet<string> _seenIds = new();
    private CancellationTokenSource? _cancel;
    private readonly TimeSpan _interval;

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

        _ = PollLoopAsync();
    }

    /// <summary>
    /// Stops listening for new messages.
    /// </summary>
    public void Stop() => _cancel?.Cancel();

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
        Stop();
    }
}
