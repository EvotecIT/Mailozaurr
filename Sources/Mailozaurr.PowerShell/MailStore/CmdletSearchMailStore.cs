using OfficeIMO.Email;
using OfficeIMO.Email.Store;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Searches mail-store metadata or selected message content.</para>
/// <para type="description">Uses OfficeIMO.Email bounded summary search by default. Supplying Term enables resumable content search across selected semantic fields.</para>
/// <example>
///   <summary>Search message metadata</summary>
///   <code>$data | Search-MailStore -SubjectContains 'invoice' -Since (Get-Date).AddYears(-1)</code>
/// </example>
/// <example>
///   <summary>Search message bodies and return matching rows</summary>
///   <code>$data | Search-MailStore -Term 'project','budget' -MatchMode AllTerms -ResultsOnly</code>
/// </example>
/// </summary>
[Cmdlet(VerbsCommon.Search, "MailStore", DefaultParameterSetName = MetadataParameterSet)]
[OutputType(typeof(EmailStoreSearchResult), typeof(EmailStoreContentSearchReport), typeof(EmailStoreContentSearchResult))]
public sealed class CmdletSearchMailStore : MailStoreCmdletBase {
    private const string MetadataParameterSet = "Metadata";
    private const string ContentParameterSet = "Content";

    /// <summary>An Import-MailData result containing a store, or a native EmailStoreSession.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [Alias("Store")]
    [ValidateNotNull]
    public object? InputObject { get; set; }

    /// <summary>Terms used for bounded content search.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ContentParameterSet)]
    [ValidateNotNullOrEmpty]
    public string[]? Term { get; set; }

    /// <summary>Optional stable folder identifier.</summary>
    [Parameter]
    public string? FolderId { get; set; }

    /// <summary>Includes descendants of FolderId.</summary>
    [Parameter]
    public SwitchParameter IncludeDescendants { get; set; }

    /// <summary>Includes folder-associated information items.</summary>
    [Parameter]
    public SwitchParameter IncludeAssociatedItems { get; set; }

    /// <summary>Includes recoverable items absent from normal folder tables.</summary>
    [Parameter]
    public SwitchParameter IncludeOrphanedItems { get; set; }

    /// <summary>Optional typed Outlook item classification.</summary>
    [Parameter]
    public OutlookItemKind? ItemKind { get; set; }

    /// <summary>Optional case-insensitive subject fragment.</summary>
    [Parameter]
    public string? SubjectContains { get; set; }

    /// <summary>Optional case-insensitive sender name or address fragment.</summary>
    [Parameter]
    public string? SenderContains { get; set; }

    /// <summary>Inclusive lower timestamp bound.</summary>
    [Parameter]
    public DateTimeOffset? Since { get; set; }

    /// <summary>Exclusive upper timestamp bound.</summary>
    [Parameter]
    public DateTimeOffset? Before { get; set; }

    /// <summary>Optional declared attachment-presence filter.</summary>
    [Parameter]
    public bool? HasAttachments { get; set; }

    /// <summary>Optional read-state filter.</summary>
    [Parameter]
    public bool? IsRead { get; set; }

    /// <summary>Maximum item references inspected.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxItemsScanned { get; set; } = 10_000;

    /// <summary>Maximum matches returned.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxResults { get; set; } = 100;

    /// <summary>Semantic fields searched when Term is supplied.</summary>
    [Parameter(ParameterSetName = ContentParameterSet)]
    public EmailStoreContentSearchFields Fields { get; set; } = EmailStoreContentSearchFields.All;

    /// <summary>Whether every term or any term must match.</summary>
    [Parameter(ParameterSetName = ContentParameterSet)]
    public EmailStoreContentMatchMode MatchMode { get; set; } = EmailStoreContentMatchMode.AllTerms;

    /// <summary>Maximum characters retained in a content-search snippet.</summary>
    [Parameter(ParameterSetName = ContentParameterSet)]
    [ValidateRange(32, int.MaxValue)]
    public int SnippetCharacters { get; set; } = 240;

    /// <summary>Writes content-search matches instead of the report containing completion and resume state.</summary>
    [Parameter(ParameterSetName = ContentParameterSet)]
    public SwitchParameter ResultsOnly { get; set; }

    /// <summary>Executes bounded metadata or content search.</summary>
    protected override Task ProcessRecordAsync() {
        try {
            EmailStoreSession session = GetStoreSession(InputObject);
            EmailStoreQuery metadata = CreateMetadataQuery();
            if (ParameterSetName == ContentParameterSet) {
                var query = new EmailStoreContentQuery(
                    Term!,
                    Fields,
                    MatchMode,
                    metadata,
                    MaxItemsScanned,
                    MaxResults,
                    snippetCharacters: SnippetCharacters);
                EmailStoreContentSearchReport report = session.SearchContent(query, cancellationToken: CancelToken);
                if (ResultsOnly.IsPresent) {
                    foreach (EmailStoreContentSearchResult result in report.Results) WriteObject(result);
                } else {
                    WriteObject(report);
                }
                WriteStoreDiagnostics(report.Diagnostics, InputObject);
            } else {
                foreach (EmailStoreSearchResult result in session.Search(metadata, CancelToken)) WriteObject(result);
            }
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "MailStoreSearchFailed", ErrorCategory.ReadError, InputObject));
        }
        return Task.CompletedTask;
    }

    private EmailStoreQuery CreateMetadataQuery() => new EmailStoreQuery(
        FolderId,
        IncludeDescendants.IsPresent,
        IncludeAssociatedItems.IsPresent,
        IncludeOrphanedItems.IsPresent,
        ItemKind,
        SubjectContains,
        SenderContains,
        Since,
        Before,
        HasAttachments,
        IsRead,
        MaxItemsScanned,
        MaxResults);
}
