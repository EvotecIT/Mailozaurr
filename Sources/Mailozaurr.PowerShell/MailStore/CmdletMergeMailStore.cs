using OfficeIMO.Email.Store;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Merges multiple read-only mail stores into a new Unicode PST.</para>
/// <para type="description">Delegates folder mapping, bounded retries, semantic deduplication, and PST writing to OfficeIMO.Email. Source PST, OST, OLM, EMLX, Mbox, and mailbox-directory data is never modified.</para>
/// <example>
///   <summary>Merge two archives while preserving separate source roots</summary>
///   <code>Merge-MailStore './old.pst','./offline.ost' './combined.pst'</code>
/// </example>
/// </summary>
[Cmdlet(VerbsData.Merge, "MailStore", SupportsShouldProcess = true)]
[OutputType(typeof(EmailStorePstMergeReport))]
public sealed class CmdletMergeMailStore : MailStoreCmdletBase {
    /// <summary>Source store files or mailbox directories.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Path")]
    [ValidateNotNullOrEmpty]
    public string[]? InputPath { get; set; }

    /// <summary>Destination Unicode PST path.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [Alias("DestinationPath")]
    [ValidateNotNullOrEmpty]
    public string? OutputPath { get; set; }

    /// <summary>Allows an existing destination PST to be atomically replaced.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <summary>Optional destination store display name.</summary>
    [Parameter]
    public string? DisplayName { get; set; }

    /// <summary>Controls whether source roots stay separate, equal paths merge, or all items are flattened.</summary>
    [Parameter]
    public EmailStoreMergeFolderMode FolderMode { get; set; } = EmailStoreMergeFolderMode.SeparateSourceRoots;

    /// <summary>Writes semantically duplicate items instead of deduplicating them.</summary>
    [Parameter]
    public SwitchParameter DisableDeduplication { get; set; }

    /// <summary>Stops after the first source-open failure.</summary>
    [Parameter]
    public SwitchParameter StopOnSourceError { get; set; }

    /// <summary>Stops after the first item-read failure.</summary>
    [Parameter]
    public SwitchParameter StopOnItemError { get; set; }

    /// <summary>Omits folder-associated information items.</summary>
    [Parameter]
    public SwitchParameter ExcludeAssociatedItems { get; set; }

    /// <summary>Omits recoverable items absent from normal folder tables.</summary>
    [Parameter]
    public SwitchParameter ExcludeOrphanedItems { get; set; }

    /// <summary>Copies search-folder results as static folders and items.</summary>
    [Parameter]
    public SwitchParameter IncludeSearchFolders { get; set; }

    /// <summary>Maximum source items inspected across the merge.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxItems { get; set; } = int.MaxValue;

    /// <summary>Runs the verified multi-source merge and returns its native OfficeIMO report.</summary>
    protected override Task ProcessRecordAsync() {
        string[]? inputPaths = InputPath;
        string? outputPath = OutputPath;
        if (inputPaths == null || inputPaths.Length == 0 || string.IsNullOrWhiteSpace(outputPath)) {
            return Task.CompletedTask;
        }
        try {
            string[] sources = inputPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(GetUnresolvedProviderPathFromPSPath)
                .ToArray();
            if (sources.Length == 0) throw new PSArgumentException("At least one source store is required.");
            string destination = GetUnresolvedProviderPathFromPSPath(outputPath);
            if (!ShouldProcess(destination, $"Merge {sources.Length} mail stores into a Unicode PST")) {
                return Task.CompletedTask;
            }
            var options = new EmailStorePstMergeOptions(
                Force.IsPresent,
                DisplayName,
                FolderMode,
                !DisableDeduplication.IsPresent,
                continueOnSourceError: !StopOnSourceError.IsPresent,
                continueOnItemError: !StopOnItemError.IsPresent,
                includeAssociatedItems: !ExcludeAssociatedItems.IsPresent,
                includeOrphanedItems: !ExcludeOrphanedItems.IsPresent,
                includeSearchFolders: IncludeSearchFolders.IsPresent,
                maxItems: MaxItems);
            EmailStoreMergeSource[] mergeSources = sources
                .Select(path => new EmailStoreMergeSource(path))
                .ToArray();
            EmailStorePstMergeReport report = EmailStoreConverter.MergeToPst(
                mergeSources,
                destination,
                options,
                CancelToken);
            WriteObject(report);
            WriteStoreDiagnostics(report.Diagnostics, destination);
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "MailStoreMergeFailed", ErrorCategory.WriteError, outputPath));
        }
        return Task.CompletedTask;
    }
}
