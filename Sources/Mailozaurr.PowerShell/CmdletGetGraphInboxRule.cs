using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Mailozaurr;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Retrieves inbox rules for a mailbox via Microsoft Graph.
/// </summary>
[Cmdlet(VerbsCommon.Get, "GraphInboxRule")]
[OutputType(typeof(GraphInboxRule))]
public sealed class CmdletGetGraphInboxRule : AsyncPSCmdlet {
    /// <summary>
    /// User principal name whose inbox rules are retrieved.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Graph")]
    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// Connection information for Microsoft Graph.
    /// </summary>
    [Parameter(ParameterSetName = "Graph", ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    /// <summary>
    /// Optional OData filter string.
    /// </summary>
    [Parameter]
    public string? Filter { get; set; }

    /// <summary>
    /// Indicates the use of Invoke-MgGraphRequest for this operation.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    [Parameter]
    public int TimeoutSeconds { get; set; } = 100;

    /// <summary>
    /// Number of retry attempts on failure.
    /// </summary>
    [Parameter]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Delay between retries in milliseconds.
    /// </summary>
    [Parameter]
    public int RetryDelayMilliseconds { get; set; } = 0;

    /// <summary>
    /// Retrieves inbox rules using Graph or Invoke-MgGraphRequest.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override Task ProcessRecordAsync() {
        if (ParameterSetName == "MgGraphRequest") {
            return ProcessMgGraph();
        }

        var conn = Connection ?? DefaultSessions.GraphSession;
        if (conn == null) {
            WriteWarning("Get-GraphInboxRule - Connection not provided and no default session available.");
            return Task.CompletedTask;
        }

        return ProcessGraphAsync(conn.Credential);
    }

    private async Task ProcessGraphAsync(GraphCredential cred) {
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                var rules = await MicrosoftGraphUtils.GetRulesAsync(cred, UserPrincipalName!, Filter);
                foreach (var r in rules) WriteObject(r);
                return;
            } catch (Exception ex) {
                lastException = ex;
                WriteWarning($"Get-GraphInboxRule - {ex.Message}");
                if (!Helpers.IsTransient(ex) || attempts >= RetryCount) {
                    if (ex is GraphApiException gex) {
                        WriteError(new ErrorRecord(gex, "GraphApiError", ErrorCategory.InvalidOperation, null));
                    } else {
                        WriteError(new ErrorRecord(ex, "GraphError", ErrorCategory.InvalidOperation, null));
                    }
                    return;
                }
                if (RetryDelayMilliseconds > 0) await Task.Delay(RetryDelayMilliseconds);
            }
            attempts++;
        } while (attempts <= RetryCount);
        if (lastException is not null) {
            WriteError(new ErrorRecord(lastException, "GraphError", ErrorCategory.InvalidOperation, null));
        }
    }

    private Task ProcessMgGraph() {
        var qp = string.IsNullOrWhiteSpace(Filter) ? null : new Dictionary<string, object> { ["$filter"] = Filter! };
        var uri = MicrosoftGraphUtils.JoinUriQuery(
            GraphEndpoint.V1,
            $"/users/{UserPrincipalName}/mailFolders/inbox/messageRules",
            qp);
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "GET")
            .AddParameter("Uri", uri);
        var results = ps.Invoke();
        foreach (var res in results) WriteObject(res);
        return Task.CompletedTask;
    }
}
