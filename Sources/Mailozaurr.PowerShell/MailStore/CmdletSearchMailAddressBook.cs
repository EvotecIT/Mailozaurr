using OfficeIMO.Email;
using OfficeIMO.Email.AddressBook;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Searches a bounded, resumable Outlook Offline Address Book.</para>
/// <para type="description">Returns OfficeIMO.Email's native report with matches, diagnostics, and a checkpoint for the next batch.</para>
/// <example>
///   <summary>Continue an address-book search</summary>
///   <code>$data = Import-MailData './directory.oab'
/// try {
///     $first = $data | Search-MailAddressBook -Term 'Ada' -MaxEntriesScanned 1000
///     if ($first.NextCheckpoint) {
///         $data | Search-MailAddressBook -Term 'Ada' -MaxEntriesScanned 1000 -ResumeFrom $first.NextCheckpoint
///     }
/// } finally { $data | Close-MailData }</code>
/// </example>
/// </summary>
[Cmdlet(VerbsCommon.Search, "MailAddressBook")]
[OutputType(typeof(OfflineAddressBookSearchReport), typeof(OfflineAddressBookSearchResult))]
public sealed class CmdletSearchMailAddressBook : MailStoreCmdletBase {
    /// <summary>Import-MailData result containing an OAB, or a native OAB session.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public object? InputObject { get; set; }

    /// <summary>One to 32 search terms.</summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string[]? Term { get; set; }

    /// <summary>Semantic fields searched.</summary>
    [Parameter]
    public OfflineAddressBookSearchFields Fields { get; set; } = OfflineAddressBookSearchFields.All;

    /// <summary>Whether all terms or any term must match.</summary>
    [Parameter]
    public OfflineAddressBookSearchMatchMode MatchMode { get; set; } = OfflineAddressBookSearchMatchMode.AllTerms;

    /// <summary>Optional address-list scope.</summary>
    [Parameter]
    public string? AddressListId { get; set; }

    /// <summary>Optional address-entry type.</summary>
    [Parameter]
    public OfflineAddressBookObjectType? ObjectType { get; set; }

    /// <summary>Maximum entries scanned in this batch.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxEntriesScanned { get; set; } = 100_000;

    /// <summary>Maximum matches returned in this batch.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxResults { get; set; } = 100;

    /// <summary>Maximum searchable characters decoded from one entry.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxSearchableCharactersPerEntry { get; set; } = 1_000_000;

    /// <summary>Maximum snippet characters.</summary>
    [Parameter]
    [ValidateRange(32, int.MaxValue)]
    public int SnippetCharacters { get; set; } = 240;

    /// <summary>Checkpoint from a previous batch with the same search terms and policy.</summary>
    [Parameter]
    public OfflineAddressBookSearchCheckpoint? ResumeFrom { get; set; }

    /// <summary>Writes matches instead of the report containing completion and resume state.</summary>
    [Parameter]
    public SwitchParameter ResultsOnly { get; set; }

    /// <summary>Executes the native bounded search.</summary>
    protected override Task ProcessRecordAsync() {
        try {
            var query = new OfflineAddressBookSearchQuery(Term!, Fields, MatchMode,
                AddressListId, ObjectType, MaxEntriesScanned, MaxResults,
                MaxSearchableCharactersPerEntry, SnippetCharacters, resumeFrom: ResumeFrom);
            var report = GetAddressBookSession(InputObject).Search(query, cancellationToken: CancelToken);
            if (ResultsOnly.IsPresent) {
                foreach (var result in report.Results) WriteObject(result);
            } else {
                WriteObject(report);
            }
            foreach (var diagnostic in report.Diagnostics) {
                var message = $"{diagnostic.Code}: {diagnostic.Message}";
                if (diagnostic.Severity == EmailDiagnosticSeverity.Error) {
                    WriteError(new ErrorRecord(new InvalidDataException(message), diagnostic.Code,
                        ErrorCategory.InvalidData, InputObject));
                } else if (diagnostic.Severity == EmailDiagnosticSeverity.Warning) {
                    WriteWarning(message);
                } else {
                    WriteVerbose(message);
                }
            }
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "MailAddressBookSearchFailed", ErrorCategory.ReadError, InputObject));
        }
        return Task.CompletedTask;
    }
}
