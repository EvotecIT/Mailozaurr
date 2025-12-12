using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Mailozaurr;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Removes an inbox rule via Microsoft Graph.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "GraphInboxRule", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class CmdletRemoveGraphInboxRule : AsyncPSCmdlet {
    /// <summary>
    /// User principal name owning the inbox rule.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// Identifier of the rule to remove.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? RuleId { get; set; }

    /// <summary>
    /// Graph connection context.
    /// </summary>
    [Parameter(ParameterSetName = "Graph", ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    /// <summary>
    /// Use <c>Invoke-MgGraphRequest</c> instead of built-in logic.
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
    /// Executes the cmdlet logic asynchronously.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        switch (ParameterSetName) {
            case "Graph":
                var conn = Connection ?? DefaultSessions.GraphSession;
                if (conn == null) {
                    WriteWarning("Remove-GraphInboxRule - Connection not provided and no default session available.");
                    return Task.CompletedTask;
                }
                return ProcessGraphAsync(conn.Credential);
            case "MgGraphRequest":
                ProcessMgGraph();
                return Task.CompletedTask;
            default:
                return Task.CompletedTask;
        }
    }

    private async Task ProcessGraphAsync(GraphCredential cred) {
        var dryRun = !ShouldProcess(RuleId!, "Removing inbox rule");
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        if (dryRun) {
            await MicrosoftGraphUtils.RemoveRuleAsync(cred, UserPrincipalName!, RuleId!, dryRun: true);
            return;
        }
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                await MicrosoftGraphUtils.RemoveRuleAsync(cred, UserPrincipalName!, RuleId!, dryRun: false);
                return;
            } catch (Exception ex) {
                lastException = ex;
                WriteWarning($"Remove-GraphInboxRule - {ex.Message}");
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

    private void ProcessMgGraph() {
        if (!ShouldProcess(RuleId!, "Removing inbox rule")) {
            return;
        }
        var uri = MicrosoftGraphUtils.JoinUriQuery(
            GraphEndpoint.V1,
            $"/users/{UserPrincipalName}/mailFolders/inbox/messageRules/{RuleId}");
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "DELETE")
            .AddParameter("Uri", uri)
            .AddParameter("ContentType", "application/json");
        ps.Invoke();
    }
}
