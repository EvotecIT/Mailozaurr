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

    /// <summary>Optional source reader limits and PST passwords. Supply one value for every source, or one value per InputPath.</summary>
    [Parameter]
    public EmailStoreReaderOptions?[]? StoreReaderOptions { get; set; }

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
            if (inputPaths.Any(string.IsNullOrWhiteSpace)) {
                throw new PSArgumentException("InputPath cannot contain an empty source path.");
            }
            string[] sources = inputPaths.Select(GetUnresolvedProviderPathFromPSPath).ToArray();
            ValidateReaderOptionCount(sources.Length);
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
                .Select((path, index) => new EmailStoreMergeSource(
                    path,
                    readerOptions: GetReaderOptions(index)))
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

    private void ValidateReaderOptionCount(int sourceCount) {
        int optionCount = StoreReaderOptions?.Length ?? 0;
        if (optionCount != 0 && optionCount != 1 && optionCount != sourceCount) {
            throw new PSArgumentException(
                "StoreReaderOptions must contain one shared value or one value for each InputPath.");
        }
    }

    private EmailStoreReaderOptions? GetReaderOptions(int index) {
        EmailStoreReaderOptions?[]? options = StoreReaderOptions;
        if (options == null || options.Length == 0) return null;
        return options.Length == 1 ? options[0] : options[index];
    }
}
