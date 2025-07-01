using System.Management.Automation;
using Mailozaurr;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Searches one or more mailboxes using Microsoft Graph.</para>
/// <para type="description">The <c>Search-GraphMailbox</c> cmdlet queries Microsoft Graph using application permissions. Provide multiple user principal names to search across several mailboxes. Results are returned as <see cref="GraphMessageInfo"/> objects.</para>
/// </summary>
[Cmdlet(VerbsCommon.Search, "GraphMailbox")]
[OutputType(typeof(GraphMessageInfo))]
public class CmdletSearchGraphMailbox : AsyncPSCmdlet {
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string[]? UserPrincipalName { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? Query { get; set; }

    [Parameter(ParameterSetName = "Graph", ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    [Parameter]
    public int From { get; set; }

    [Parameter]
    public int Size { get; set; } = 25;

    protected override async Task ProcessRecordAsync() {
        var conn = Connection ?? DefaultSessions.GraphSession;
        if (conn == null) {
            WriteWarning("Search-GraphMailbox - Connection not provided and no default session available.");
            return;
        }

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
