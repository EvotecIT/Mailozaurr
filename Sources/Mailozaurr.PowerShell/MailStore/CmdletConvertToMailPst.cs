using OfficeIMO.Email.Store;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Converts a supported mail store into a new Unicode PST.</para>
/// <para type="description">Converts PST, supported PST-compatible OST, OLM, Mbox, EMLX, EML, Maildir, Apple Mail, or EML directory sources through OfficeIMO.Email. The source is never modified and semantic verification is enabled by default.</para>
/// <example>
///   <summary>Convert an OST to a verified Unicode PST</summary>
///   <code>ConvertTo-MailPst './offline.ost' './portable.pst'</code>
/// </example>
/// </summary>
[Cmdlet(VerbsData.ConvertTo, "MailPst", SupportsShouldProcess = true)]
[OutputType(typeof(EmailStorePstConversionReport))]
public sealed class CmdletConvertToMailPst : MailStoreCmdletBase {
    /// <summary>Source PST, OST, OLM, Mbox, EMLX, EML, or mailbox-directory path.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Path", "FullName")]
    [ValidateNotNullOrEmpty]
    public string? InputPath { get; set; }

    /// <summary>Destination Unicode PST path.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [Alias("DestinationPath")]
    [ValidateNotNullOrEmpty]
    public string? OutputPath { get; set; }

    /// <summary>Optional source reader limits and PST password.</summary>
    [Parameter]
    public EmailStoreReaderOptions? StoreReaderOptions { get; set; }

    /// <summary>Allows an existing destination PST to be atomically replaced.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <summary>Blocks completion when conversion or verification reports semantic loss.</summary>
    [Parameter]
    public SwitchParameter FailOnDataLoss { get; set; }

    /// <summary>Stops after the first unreadable source item.</summary>
    [Parameter]
    public SwitchParameter StopOnItemError { get; set; }

    /// <summary>Omits folder-associated information items.</summary>
    [Parameter]
    public SwitchParameter ExcludeAssociatedItems { get; set; }

    /// <summary>Omits recoverable items absent from normal folder tables.</summary>
    [Parameter]
    public SwitchParameter ExcludeOrphanedItems { get; set; }

    /// <summary>Omits search-folder results instead of copying them as static folders.</summary>
    [Parameter]
    public SwitchParameter ExcludeSearchFolders { get; set; }

    /// <summary>Maximum source items inspected.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxItems { get; set; } = int.MaxValue;

    /// <summary>Maximum embedded-message nesting depth written.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int MaxNestedMessageDepth { get; set; } = 32;

    /// <summary>Optional destination store display name.</summary>
    [Parameter]
    public string? DisplayName { get; set; }

    /// <summary>Skips the default staged semantic verification before committing the destination.</summary>
    [Parameter]
    public SwitchParameter SkipVerification { get; set; }

    /// <summary>Runs a read-only source conversion and returns its native OfficeIMO report.</summary>
    protected override Task ProcessRecordAsync() {
        string? inputPath = InputPath;
        string? outputPath = OutputPath;
        if (string.IsNullOrWhiteSpace(inputPath) || string.IsNullOrWhiteSpace(outputPath)) {
            return Task.CompletedTask;
        }
        try {
            string source = GetUnresolvedProviderPathFromPSPath(inputPath);
            string destination = GetUnresolvedProviderPathFromPSPath(outputPath);
            if (!ShouldProcess(destination, $"Convert '{source}' to a Unicode PST")) return Task.CompletedTask;
            var options = new EmailStorePstConversionOptions(
                Force.IsPresent,
                FailOnDataLoss.IsPresent,
                !StopOnItemError.IsPresent,
                !ExcludeAssociatedItems.IsPresent,
                !ExcludeOrphanedItems.IsPresent,
                !ExcludeSearchFolders.IsPresent,
                MaxItems,
                MaxNestedMessageDepth,
                DisplayName,
                !SkipVerification.IsPresent);
            EmailStorePstConversionReport report = EmailStoreConverter.ConvertToPst(
                source,
                destination,
                readerOptions: StoreReaderOptions,
                conversionOptions: options,
                cancellationToken: CancelToken);
            WriteObject(report);
            WriteStoreDiagnostics(report.Diagnostics, destination);
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "MailPstConversionFailed", ErrorCategory.WriteError, outputPath));
        }
        return Task.CompletedTask;
    }
}
