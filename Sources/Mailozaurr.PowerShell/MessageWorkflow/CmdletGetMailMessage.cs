using OfficeIMO.Email;

namespace Mailozaurr.PowerShell;

using EmailMessage = OfficeIMO.Email.EmailMessage;

/// <summary>
/// <para type="synopsis">Reads messages from local email files or offline archives.</para>
/// <para type="description">Returns bodies, recipient addresses and attachment descriptions without leaving a source open. First limits matching messages across all input paths. MaxItemsScanned limits summaries examined per source and emits a warning when reached. Payload operations require the original source to remain unchanged.</para>
/// <example><summary>Read one matching invoice</summary><code>$mail = Get-MailMessage -Path './Archive.pst' -SubjectContains 'invoice' -First 1</code></example>
/// <example><summary>Read EML and MSG files</summary><code>Get-MailMessage -Path './Mail/*.eml', './Mail/*.msg'</code></example>
/// </summary>
[Cmdlet(VerbsCommon.Get, "MailMessage", DefaultParameterSetName = "Store")]
[OutputType(typeof(EmailMessage))]
public sealed class CmdletGetMailMessage : AsyncPSCmdlet {
    private int _returned;
    /// <summary>Local email or archive paths; wildcard patterns are supported.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Path")]
    [Alias("FullName")]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = Array.Empty<string>();
    /// <summary>Store scope supplied by Invoke-MailStore.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Store", ValueFromPipeline = true)]
    [ValidateNotNull]
    public object? InputObject { get; set; }
    /// <summary>Exact folder path or a unique folder name.</summary>
    [Parameter]
    public string? Folder { get; set; }
    /// <summary>Includes descendants of Folder.</summary>
    [Parameter]
    public SwitchParameter Recurse { get; set; }
    /// <summary>Case-insensitive subject fragment.</summary>
    [Parameter]
    public string? SubjectContains { get; set; }
    /// <summary>Case-insensitive sender name or address fragment.</summary>
    [Parameter]
    public string? SenderContains { get; set; }
    /// <summary>Inclusive received-date lower bound, falling back to sent date.</summary>
    [Parameter]
    public DateTimeOffset? Since { get; set; }
    /// <summary>Exclusive received-date upper bound, falling back to sent date.</summary>
    [Parameter]
    public DateTimeOffset? Before { get; set; }
    /// <summary>Maximum matching messages across all input sources. Default: 1000.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int First { get; set; } = 1000;
    /// <summary>Maximum summaries examined per source. Default: 1000000.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxItemsScanned { get; set; } = 1_000_000;

    /// <summary>Reads the current input scope.</summary>
    protected override Task ProcessRecordAsync() {
        if (_returned >= First) return Task.CompletedTask;
        var query = new EmailMessageQuery { Folder = Folder, Recurse = Recurse.IsPresent,
            SubjectContains = SubjectContains, SenderContains = SenderContains, Since = Since, Before = Before,
            First = First - _returned, MaxItemsScanned = MaxItemsScanned };
        if (InputObject != null) {
            object value = InputObject is PSObject ps ? ps.BaseObject : InputObject;
            if (value is not EmailMessageStore store) throw new PSArgumentException("InputObject must be the store supplied by Invoke-MailStore.");
            Read(() => store.Read(query, CancelToken), store.SourcePath);
        }
        else foreach (string pattern in Path) {
            if (_returned >= First) break;
            try {
                foreach (string source in GetResolvedProviderPathFromPSPath(pattern, out ProviderInfo provider)) {
                    if (provider.Name != "FileSystem") throw new PSArgumentException("A FileSystem path is required.");
                    if (_returned >= First) break;
                    query.First = First - _returned;
                    Read(() => EmailMessageReader.Read(source, query, CancelToken), source);
                }
            } catch (Exception exception) when (exception is not OperationCanceledException && exception is not PipelineStoppedException) {
                WriteError(new ErrorRecord(exception, "MailMessagePathInvalid", ErrorCategory.InvalidArgument, pattern));
            }
        }
        return Task.CompletedTask;
    }
    private void Read(Func<EmailMessageReadResult> operation, string source) {
        try {
            EmailMessageReadResult result = operation();
            if (result.StoppedAtScanLimit) WriteWarning($"Search of '{source}' reached MaxItemsScanned ({MaxItemsScanned}). Results may be incomplete.");
            foreach (EmailMessage message in result.Messages) { WriteObject(message); _returned++; }
        } catch (Exception exception) when (exception is not OperationCanceledException && exception is not PipelineStoppedException) {
            WriteError(new ErrorRecord(exception, "MailMessageReadFailed", ErrorCategory.ReadError, source));
        }
    }
}
