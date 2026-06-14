using MailKit.Search;
using System;
using System.Management.Automation;
using System.Threading;
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
    /// Additional MailKit search queries applied when listening for messages.
    /// </summary>
    [Parameter]
    public SearchQuery[]? SearchQuery { get; set; }

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
    /// Stops listening when the <c>Until</c> condition is satisfied.
    /// </summary>
    [Parameter]
    public SwitchParameter StopOnMatch { get; set; }

    /// <summary>
    /// Optional timeout after which the cmdlet stops waiting.
    /// </summary>
    [Parameter]
    public int TimeoutSeconds { get; set; }

    private ImapIdleListener? _listener;
    private CancellationTokenSource? _timeoutSource;
    private CancellationTokenSource? _linkedSource;

    /// <inheritdoc />
    /// <summary>
    /// Begins listening for new IMAP messages using the IDLE command.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn == null || conn.Data == null) {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Wait-IMAPMessage - IMAP client not provided or not connected."),
                "ClientNotConnected",
                ErrorCategory.InvalidOperation,
                null));
            return;
        }

        SearchQuery? query = null;
        if (SearchQuery != null && SearchQuery.Length > 0) {
            query = SearchQuery[0];
            for (int i = 1; i < SearchQuery.Length; i++) {
                query = query.And(SearchQuery[i]);
            }
        }

        _listener = new ImapIdleListener(conn.Data, Folder, query);
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

    private void OnMessageArrived(object? sender, ImapEmailMessage message) {
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