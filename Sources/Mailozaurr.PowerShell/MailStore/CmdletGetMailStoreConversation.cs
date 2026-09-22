using OfficeIMO.Email.Store;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Builds a bounded conversation graph from an imported mail store.</para>
/// <para type="description">Returns OfficeIMO.Email's native graph, including edge confidence and diagnostics.</para>
/// <example>
///   <summary>Inspect conversations in a PST</summary>
///   <code>$data = Import-MailData './archive.pst'
/// try { $data | Get-MailStoreConversation } finally { $data | Close-MailData }</code>
/// </example>
/// </summary>
[Cmdlet(VerbsCommon.Get, "MailStoreConversation")]
[OutputType(typeof(EmailConversationGraph))]
public sealed class CmdletGetMailStoreConversation : MailStoreCmdletBase {
    /// <summary>Import-MailData result containing a store, or a native session.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public object? InputObject { get; set; }

    /// <summary>Optional OfficeIMO conversation scope, bounds, and heuristic policy.</summary>
    [Parameter]
    public EmailConversationGraphOptions? Options { get; set; }

    /// <summary>Produces the native conversation graph.</summary>
    protected override Task ProcessRecordAsync() {
        try {
            WriteObject(GetStoreSession(InputObject).BuildConversationGraph(Options, CancelToken));
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "MailStoreConversationFailed", ErrorCategory.ReadError, InputObject));
        }
        return Task.CompletedTask;
    }
}
