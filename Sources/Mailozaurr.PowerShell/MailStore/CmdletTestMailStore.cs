using OfficeIMO.Email.Store;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Runs bounded validation against an imported mail store.</para>
/// <para type="description">Validates PST, OST, OLM, EMLX, Mbox, or mailbox-directory data at shallow, summary, or full-item depth. Structural PST/OST page and block verification is opt-in.</para>
/// <example>
///   <summary>Validate summaries and PST/OST structure</summary>
///   <code>$report = $data | Test-MailStore -VerifyStructuralIntegrity
/// $report.IsValid</code>
/// </example>
/// </summary>
[Cmdlet(VerbsDiagnostic.Test, "MailStore")]
[OutputType(typeof(EmailStoreValidationReport))]
public sealed class CmdletTestMailStore : MailStoreCmdletBase {
    /// <summary>An Import-MailData result containing a store, or a native EmailStoreSession.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [Alias("Store")]
    [ValidateNotNull]
    public object? InputObject { get; set; }

    /// <summary>Validation depth.</summary>
    [Parameter]
    public EmailStoreValidationMode Mode { get; set; } = EmailStoreValidationMode.Summaries;

    /// <summary>Optional stable folder identifier.</summary>
    [Parameter]
    public string? FolderId { get; set; }

    /// <summary>Includes descendants of FolderId.</summary>
    [Parameter]
    public SwitchParameter IncludeDescendants { get; set; }

    /// <summary>Includes folder-associated information items.</summary>
    [Parameter]
    public SwitchParameter IncludeAssociatedItems { get; set; }

    /// <summary>Excludes recoverable items absent from normal folder tables.</summary>
    [Parameter]
    public SwitchParameter ExcludeOrphanedItems { get; set; }

    /// <summary>Maximum item references validated.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxItems { get; set; } = 100_000;

    /// <summary>Verifies PST/OST structural page and block integrity within bounded limits.</summary>
    [Parameter]
    public SwitchParameter VerifyStructuralIntegrity { get; set; }

    /// <summary>Returns the native OfficeIMO validation report.</summary>
    protected override Task ProcessRecordAsync() {
        try {
            EmailStoreSession session = GetStoreSession(InputObject);
            var options = new EmailStoreValidationOptions(
                Mode,
                FolderId,
                IncludeDescendants.IsPresent,
                IncludeAssociatedItems.IsPresent,
                !ExcludeOrphanedItems.IsPresent,
                MaxItems,
                VerifyStructuralIntegrity.IsPresent);
            EmailStoreValidationReport report = session.Validate(options, CancelToken);
            WriteObject(report);
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "MailStoreValidationFailed", ErrorCategory.InvalidData, InputObject));
        }
        return Task.CompletedTask;
    }
}
