using OfficeIMO.Email.Store;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Converts a supported mail store into a new Unicode PST.</para>
/// <para type="description">Converts PST, supported PST-compatible OST, OLM, Mbox, EMLX, EML, Maildir, Apple Mail, or EML directory sources through OfficeIMO.Email. The source is never modified and semantic verification is enabled by default.</para>
/// <para type="description">SplitSizeMB writes numbered parts (name.part001.pst, name.part002.pst, ...) of at most that estimated size instead of one PST, in received-time order. Every part is verified against the source before it is committed, and an EmailStorePstSplitReport is returned. DisplayName and SkipVerification do not apply to a split.</para>
/// <example>
///   <summary>Convert an OST to a verified Unicode PST</summary>
///   <code>ConvertTo-MailPst './offline.ost' './portable.pst'</code>
/// </example>
/// <example>
///   <summary>Split a large PST into 2 GB parts</summary>
///   <code>ConvertTo-MailPst './archive.pst' './archive-split.pst' -SplitSizeMB 2048</code>
///   <para>Writes archive-split.part001.pst, archive-split.part002.pst and so on.</para>
/// </example>
/// </summary>
[Cmdlet(VerbsData.ConvertTo, "MailPst", SupportsShouldProcess = true)]
[OutputType(typeof(EmailStorePstConversionReport), typeof(EmailStorePstSplitReport))]
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

    /// <summary>Largest estimated size of one PST part in megabytes. Writes numbered parts instead of one PST.</summary>
    [Parameter]
    [ValidateRange(1, 1_000_000)]
    public int SplitSizeMB { get; set; }

    /// <summary>Largest number of parts written with SplitSizeMB.</summary>
    [Parameter]
    [ValidateRange(1, 10_000)]
    public int MaxParts { get; set; } = 1000;

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
            if (SplitSizeMB > 0) {
                if (!ShouldProcess(destination, $"Split '{source}' into Unicode PST parts of up to {SplitSizeMB} MB")) return Task.CompletedTask;
                if (!string.IsNullOrEmpty(DisplayName)) WriteWarning("DisplayName does not apply when SplitSizeMB is used.");
                if (SkipVerification.IsPresent) WriteVerbose("Split parts are always verified; SkipVerification is ignored.");
                using EmailStoreSession session = EmailStoreSession.Open(source, StoreReaderOptions, CancelToken);
                EmailStorePstSplitReport splitReport = session.SplitToPst(destination, BuildSplitOptions(), CancelToken);
                WriteObject(splitReport);
                WriteStoreDiagnostics(splitReport.NativeDiagnostics, destination);
                return Task.CompletedTask;
            }
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
            WriteStoreDiagnostics(report.NativeDiagnostics, destination);
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "MailPstConversionFailed", ErrorCategory.WriteError, outputPath));
        }
        return Task.CompletedTask;
    }

    private EmailStorePstSplitOptions BuildSplitOptions() {
        var query = new EmailStoreTableQuery(
            includeAssociatedItems: !ExcludeAssociatedItems.IsPresent,
            includeOrphanedItems: !ExcludeOrphanedItems.IsPresent,
            sorts: new[] {
                EmailStoreFields.ReceivedAt.Ascending(),
                EmailStoreFields.SentAt.Ascending()
            },
            maxItemsScanned: MaxItems,
            pageSize: 1000);
        return new EmailStorePstSplitOptions(
            query: query,
            maxEstimatedBytesPerPart: SplitSizeMB * 1024L * 1024L,
            maxParts: MaxParts,
            overwriteExisting: Force.IsPresent,
            failOnLoss: FailOnDataLoss.IsPresent,
            continueOnItemError: !StopOnItemError.IsPresent,
            includeSearchFolders: !ExcludeSearchFolders.IsPresent,
            maxNestedMessageDepth: MaxNestedMessageDepth);
    }
}
