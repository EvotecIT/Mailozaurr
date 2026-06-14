using Mailozaurr;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Searches one or more mailboxes using Microsoft Graph.</para>
/// <para type="description">The <c>Search-GraphMailbox</c> cmdlet queries Microsoft Graph using application permissions. Provide multiple user principal names to search across several mailboxes. Results are returned as <see cref="GraphMessageInfo"/> objects.</para>
/// </summary>
[Cmdlet(VerbsCommon.Search, "GraphMailbox")]
[OutputType(typeof(GraphMessageInfo))]
public class CmdletSearchGraphMailbox : AsyncPSCmdlet {
    /// <summary>
    /// User principal names to search across.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string[]? UserPrincipalName { get; set; }

    /// <summary>
    /// Query string used to filter messages.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? Query { get; set; }

    /// <summary>
    /// Graph connection information.
    /// </summary>
    [Parameter(ParameterSetName = "Graph", ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    /// <summary>
    /// Message index to start from.
    /// </summary>
    [Parameter]
    public int From { get; set; }

    /// <summary>
    /// Number of messages to retrieve.
    /// </summary>
    [Parameter]
    [Alias("Count")]
    [ValidateRange(1, int.MaxValue)]
    public int Size { get; set; } = 25;

    /// <summary>
    /// Maximum number of parallel Graph requests.
    /// </summary>
    [Parameter]
    public int MaxConcurrentRequests { get; set; } = 5;

    /// <summary>
    /// Executes the cmdlet logic asynchronously.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        var conn = Connection ?? DefaultSessions.GraphSession;
        if (conn == null) {
            WriteWarning("Search-GraphMailbox - Connection not provided and no default session available.");
            return;
        }

        MicrosoftGraphUtils.MaxConcurrentRequests = MaxConcurrentRequests;
        try {
            var results = await MicrosoftGraphUtils.SearchMailboxesAsync(
                conn.Credential,
                UserPrincipalName!,
                Query!,
                From,
                Size);

            foreach (var info in results) {
                WriteObject(info);
            }
        } catch (GraphApiException ex) {
            WriteError(new ErrorRecord(ex, "GraphApiError", ErrorCategory.InvalidOperation, null));
        }
    }
}