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
    private CancellationTokenSource? _matchSource;
    private CancellationTokenSource? _linkedSource;
    private int _matchSignaled;

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
        try {
            _listener.MessageArrived += OnMessageArrived;
            Volatile.Write(ref _matchSignaled, 0);
            _matchSource = new CancellationTokenSource();
            await _listener.StartAsync(CancelToken);
            _timeoutSource = TimeoutSeconds > 0
                ? new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds))
                : null;
            _linkedSource = _timeoutSource == null
                ? CancellationTokenSource.CreateLinkedTokenSource(CancelToken, _matchSource.Token)
                : CancellationTokenSource.CreateLinkedTokenSource(CancelToken, _timeoutSource.Token, _matchSource.Token);

            try {
                await Task.Delay(-1, _linkedSource.Token);
            } catch (TaskCanceledException) { }
        } finally {
            DisposeRecordResources();
        }
    }

    private void OnMessageArrived(object? sender, Pop3EmailMessage message) {
        var match = Until == null || LanguagePrimitives.IsTrue(Until.InvokeReturnAsIs(message));
        if (match) {
            if (StopOnMatch && Interlocked.Exchange(ref _matchSignaled, 1) != 0)
                return;

            WriteObject(message);
            if (Action != null) {
                try {
                    Action.Invoke(message);
                } catch (RuntimeException ex) {
                    WriteError(ex.ErrorRecord);
                }
            }
            if (StopOnMatch) {
                _matchSource?.Cancel();
            }
        }
    }

    /// <inheritdoc />
    protected override Task EndProcessingAsync() {
        DisposeRecordResources();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Dispose() {
        DisposeRecordResources();
        base.Dispose();
    }

    private void DisposeRecordResources() {
        var listener = Interlocked.Exchange(ref _listener, null);
        if (listener != null) {
            listener.MessageArrived -= OnMessageArrived;
            listener.Dispose();
        }
        Interlocked.Exchange(ref _linkedSource, null)?.Dispose();
        Interlocked.Exchange(ref _timeoutSource, null)?.Dispose();
        Interlocked.Exchange(ref _matchSource, null)?.Dispose();
    }

    /// <inheritdoc />
    protected override void StopProcessing() {
        _listener?.Stop();
        _timeoutSource?.Cancel();
        _matchSource?.Cancel();
        _linkedSource?.Cancel();
        base.StopProcessing();
    }
}
