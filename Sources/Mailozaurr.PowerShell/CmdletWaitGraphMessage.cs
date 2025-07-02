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
    [Parameter(ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    [Parameter]
    public ScriptBlock? Action { get; set; }

    [Parameter]
    public ScriptBlock? Until { get; set; }

    [Parameter]
    public SwitchParameter StopOnMatch { get; set; }

    [Parameter]
    public int TimeoutSeconds { get; set; }

    private GraphMessageListener? _listener;
    private CancellationTokenSource? _timeoutSource;
    private CancellationTokenSource? _linkedSource;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        var conn = Connection ?? DefaultSessions.GraphSession;
        if (conn == null || conn.Credential == null) {
            WriteWarning("Wait-GraphMessage - Connection not provided and no default session available.");
            return;
        }

        _listener = new GraphMessageListener(conn.Credential, UserPrincipalName!);
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

    private void OnMessageArrived(object? sender, System.Collections.Generic.Dictionary<string, object> message) {
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
        _listener?.Dispose();
        _timeoutSource?.Dispose();
        _linkedSource?.Dispose();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose() {
        _listener?.Dispose();
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
