using System;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Waits for new POP3 messages by polling the server.</para>
/// <para type="description">The <c>Wait-POP3Message</c> cmdlet listens for new messages arriving in the connected POP3 mailbox. Messages are written to the pipeline as they are received.</para>
/// </summary>
[Cmdlet(VerbsLifecycle.Wait, "POP3Message")]
[OutputType(typeof(Pop3EmailMessage))]
public sealed class CmdletWaitPOP3Message : AsyncPSCmdlet, IDisposable {
    /// <summary>
    /// Connection information for the POP3 session used for polling.
    /// </summary>
    [Parameter(ValueFromPipeline = true)]
    [ValidateNotNull]
    public PopConnectionInfo? Client { get; set; }

    /// <summary>
    /// Optional action invoked for each message that arrives.
    /// </summary>
    [Parameter]
    public ScriptBlock? Action { get; set; }

    /// <summary>
    /// Script block determining when to stop waiting for messages.
    /// </summary>
    [Parameter]
    public ScriptBlock? Until { get; set; }

    /// <summary>
    /// Stops polling when the <c>Until</c> condition is satisfied.
    /// </summary>
    [Parameter]
    public SwitchParameter StopOnMatch { get; set; }

    /// <summary>
    /// Optional timeout after which the cmdlet stops polling.
    /// </summary>
    [Parameter]
    public int TimeoutSeconds { get; set; }

    private Pop3PollListener? _listener;
    private CancellationTokenSource? _timeoutSource;
    private CancellationTokenSource? _linkedSource;

    /// <inheritdoc />
    /// <summary>
    /// Begins polling the POP3 mailbox for new messages until stopped.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.Pop3Session;
        if (conn == null || conn.Data == null) {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Wait-POP3Message - POP3 client not provided or not connected."),
                "ClientNotConnected",
                ErrorCategory.InvalidOperation,
                null));
            return;
        }

        _listener = new Pop3PollListener(conn.Data);
        _listener.MessageArrived += OnMessageArrived;
        await _listener.StartAsync(CancelToken);

        CancellationToken token = CancelToken;
        if (TimeoutSeconds > 0) {
            _timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            _linkedSource = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, _timeoutSource.Token);
            token = _linkedSource.Token;
        }

        try {
            await Task.Delay(-1, token);
        } catch (TaskCanceledException) { }
    }

    private void OnMessageArrived(object? sender, Pop3EmailMessage message) {
        var match = Until == null || LanguagePrimitives.IsTrue(Until.InvokeReturnAsIs(message));
        if (match) {
            WriteObject(message);
            if (Action != null) {
                try {
                    Action.Invoke(message);
                } catch (RuntimeException ex) {
                    WriteError(ex.ErrorRecord);
                }
            }
            if (StopOnMatch) {
                StopProcessing();
            }
        }
    }

    /// <inheritdoc />
    protected override Task EndProcessingAsync() {
        if (_listener != null) {
            _listener.MessageArrived -= OnMessageArrived;
            _listener.Dispose();
        }
        _timeoutSource?.Dispose();
        _linkedSource?.Dispose();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public new void Dispose() {
        if (_listener != null) {
            _listener.MessageArrived -= OnMessageArrived;
            _listener.Dispose();
        }
        _timeoutSource?.Dispose();
        _linkedSource?.Dispose();
    }

    /// <inheritdoc />
    protected override void StopProcessing() {
        _listener?.Stop();
        _timeoutSource?.Cancel();
        _linkedSource?.Cancel();
        base.StopProcessing();
    }
}
