using System;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Waits for new Graph messages by polling Microsoft Graph.</para>
/// <para type="description">The <c>Wait-GraphMessage</c> cmdlet listens for new messages for the specified user principal name. Messages are written to the pipeline as they arrive.</para>
/// <example>
///   <summary>Listen for new messages</summary>
///   <code>
/// $listener = Wait-GraphMessage -Connection $graph -UserPrincipalName 'user@example.com'
///   </code>
/// </example>
/// </summary>
[Cmdlet(VerbsLifecycle.Wait, "GraphMessage")]
[OutputType(typeof(object))]
public sealed class CmdletWaitGraphMessage : AsyncPSCmdlet, IDisposable {
    /// <summary>
    /// Graph connection information used when polling for messages.
    /// </summary>
    [Parameter(ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    /// <summary>
    /// User principal name whose mailbox should be monitored.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// Optional action executed for each received message.
    /// </summary>
    [Parameter]
    public ScriptBlock? Action { get; set; }

    /// <summary>
    /// Script block that determines when to stop waiting for messages.
    /// </summary>
    [Parameter]
    public ScriptBlock? Until { get; set; }

    /// <summary>
    /// Stops waiting when a message matches the <c>Until</c> condition.
    /// </summary>
    [Parameter]
    public SwitchParameter StopOnMatch { get; set; }

    /// <summary>
    /// Optional timeout after which listening will stop automatically.
    /// </summary>
    [Parameter]
    public int TimeoutSeconds { get; set; }

    private GraphMessageListener? _listener;
    private CancellationTokenSource? _timeoutSource;
    private CancellationTokenSource? _matchSource;
    private CancellationTokenSource? _linkedSource;
    private readonly object _recordResourceLock = new();
    private int _matchSignaled;

    /// <inheritdoc />
    /// <summary>
    /// Starts listening for new Graph messages until stopped or timed out.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        var conn = Connection ?? DefaultSessions.GraphSession;
        if (conn == null || conn.Credential == null) {
            WriteWarning("Wait-GraphMessage - Connection not provided and no default session available.");
            return;
        }

        _listener = new GraphMessageListener(conn.Credential, UserPrincipalName!);
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

    private void OnMessageArrived(object? sender, System.Collections.Generic.Dictionary<string, object> message) {
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
                lock (_recordResourceLock) {
                    _matchSource?.Cancel();
                }
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
        GraphMessageListener? listener;
        CancellationTokenSource? linkedSource;
        CancellationTokenSource? timeoutSource;
        CancellationTokenSource? matchSource;
        lock (_recordResourceLock) {
            listener = Interlocked.Exchange(ref _listener, null);
            linkedSource = Interlocked.Exchange(ref _linkedSource, null);
            timeoutSource = Interlocked.Exchange(ref _timeoutSource, null);
            matchSource = Interlocked.Exchange(ref _matchSource, null);
        }
        if (listener != null) {
            listener.MessageArrived -= OnMessageArrived;
            listener.Dispose();
        }
        linkedSource?.Dispose();
        timeoutSource?.Dispose();
        matchSource?.Dispose();
    }

    /// <inheritdoc />
    protected override void StopProcessing() {
        lock (_recordResourceLock) {
            _listener?.Stop();
            _timeoutSource?.Cancel();
            _matchSource?.Cancel();
            _linkedSource?.Cancel();
        }
        base.StopProcessing();
    }
}
