using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Waits for new IMAP messages using the IMAP IDLE command.</para>
/// <para type="description">The <c>Wait-IMAPMessage</c> cmdlet listens for new messages arriving in the specified folder and writes them to the pipeline as they are received.</para>
/// <example>
///   <summary>Listen for new messages in the inbox</summary>
///   <code>
/// $listener = Wait-IMAPMessage -Client $client
///   </code>
/// </example>
/// <remarks>
/// Use Ctrl+C to stop waiting for messages. The cmdlet relies on a connected <see cref="ImapConnectionInfo"/> object from <c>Connect-IMAP</c>.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsLifecycle.Wait, "IMAPMessage")]
[OutputType(typeof(ImapEmailMessage))]
public sealed class CmdletWaitIMAPMessage : AsyncPSCmdlet, System.IDisposable {
    /// <summary>
    /// <para type="description">The <see cref="ImapConnectionInfo"/> representing the active IMAP session. Defaults to the last session created by <c>Connect-IMAP</c>.</para>
    /// </summary>
    [Parameter(ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>
    /// <para type="description">Optional folder name to monitor. Defaults to Inbox.</para>
    /// </summary>
    [Parameter]
    public string? Folder { get; set; }

    /// <summary>
    /// <para type="description">Optional script block invoked for each arriving message.</para>
    /// </summary>
    [Parameter]
    public ScriptBlock? Action { get; set; }

    private ImapIdleListener? _listener;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn == null || conn.Data == null) {
            WriteWarning("Wait-IMAPMessage - Is IMAP connected?");
            return;
        }

        _listener = new ImapIdleListener(conn.Data, Folder);
        _listener.MessageArrived += OnMessageArrived;
        await _listener.StartAsync(CancelToken);
        try {
            await Task.Delay(-1, CancelToken);
        } catch (TaskCanceledException) { }
    }

    private void OnMessageArrived(object? sender, ImapEmailMessage message) {
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
