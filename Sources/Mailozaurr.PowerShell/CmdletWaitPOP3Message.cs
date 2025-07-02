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
    [Parameter(ValueFromPipeline = true)]
    [ValidateNotNull]
    public PopConnectionInfo? Client { get; set; }

    [Parameter]
    public ScriptBlock? Action { get; set; }

    private Pop3PollListener? _listener;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.Pop3Session;
        if (conn == null || conn.Data == null) {
            WriteWarning("Wait-POP3Message - Is POP3 connected?");
            return;
        }

        _listener = new Pop3PollListener(conn.Data);
        _listener.MessageArrived += OnMessageArrived;
        await _listener.StartAsync(CancelToken);
        try {
            await Task.Delay(-1, CancelToken);
        } catch (TaskCanceledException) { }
    }

    private void OnMessageArrived(object? sender, Pop3EmailMessage message) {
        WriteObject(message);
        if (Action != null) {
            try {
                Action.Invoke(message);
            } catch (RuntimeException ex) {
                WriteError(ex.ErrorRecord);
            }
        }
    }

    /// <inheritdoc />
    protected override Task EndProcessingAsync() {
        _listener?.Dispose();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose() => _listener?.Dispose();

    /// <inheritdoc />
    protected override void StopProcessing() {
        _listener?.Stop();
        base.StopProcessing();
    }
}
