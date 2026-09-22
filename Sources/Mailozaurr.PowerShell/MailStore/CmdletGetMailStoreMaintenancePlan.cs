using OfficeIMO.Email.Store;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Plans read-only maintenance for an imported mail store.</para>
/// <para type="description">Returns OfficeIMO.Email's source-bound validation, recovery evidence, and recommendations. The plan does not modify the store.</para>
/// <example>
///   <summary>Inspect a PST before repair or export</summary>
///   <code>$data = Import-MailData './archive.pst'
/// try { $data | Get-MailStoreMaintenancePlan -MaxItems 50000 }
/// finally { $data | Close-MailData }</code>
/// </example>
/// </summary>
[Cmdlet(VerbsCommon.Get, "MailStoreMaintenancePlan")]
[OutputType(typeof(EmailStoreMaintenancePlan))]
public sealed class CmdletGetMailStoreMaintenancePlan : MailStoreCmdletBase {
    /// <summary>Import-MailData result containing a store, or a native session.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public object? InputObject { get; set; }

    /// <summary>Maximum item references inspected while planning.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxItems { get; set; } = 100_000;

    /// <summary>Produces the native source-bound plan.</summary>
    protected override Task ProcessRecordAsync() {
        try {
            WriteObject(GetStoreSession(InputObject).PlanMaintenance(MaxItems, CancelToken));
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "MailStoreMaintenancePlanFailed", ErrorCategory.ReadError, InputObject));
        }
        return Task.CompletedTask;
    }
}
