using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Removes mailbox permissions via Microsoft Graph.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "GraphMailboxPermission", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public class CmdletRemoveGraphMailboxPermission : AsyncPSCmdlet {
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    [Parameter(ParameterSetName = "Graph")]
    [ValidateNotNullOrEmpty]
    public string? PermissionId { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Csv")]
    [ValidateNotNullOrEmpty]
    public string? CsvPath { get; set; }

    [Parameter(ParameterSetName = "Graph", ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    [Parameter]
    public int TimeoutSeconds { get; set; } = 100;

    [Parameter]
    public int RetryCount { get; set; } = 0;

    [Parameter]
    public int RetryDelayMilliseconds { get; set; } = 0;

    protected override Task ProcessRecordAsync() {
        if (ParameterSetName == "MgGraphRequest") {
            ProcessMgGraph();
            return Task.CompletedTask;
        }

        var conn = Connection ?? DefaultSessions.GraphSession;
        if (conn == null) {
            WriteWarning("Remove-GraphMailboxPermission - Connection not provided and no default session available.");
            return Task.CompletedTask;
        }

        if (ParameterSetName == "Csv") {
            return ProcessCsvAsync(conn.Credential);
        }

        return ProcessGraphAsync(conn.Credential, PermissionId!);
    }

    private async Task ProcessCsvAsync(GraphCredential cred) {
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Import-Csv").AddParameter("Path", CsvPath);
        var rows = ps.Invoke();
        foreach (var row in rows.OfType<PSObject>()) {
            var id = row.Properties["PermissionId"].Value?.ToString();
            if (!string.IsNullOrWhiteSpace(id))
                await ProcessGraphAsync(cred, id).ConfigureAwait(false);
        }
    }

    private async Task ProcessGraphAsync(GraphCredential cred, string permissionId) {
        if (!ShouldProcess(permissionId, "Removing mailbox permission")) return;
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                await MicrosoftGraphUtils.RemoveMailboxPermissionAsync(cred, UserPrincipalName!, permissionId);
                return;
            } catch (Exception ex) {
                lastException = ex;
                WriteWarning($"Remove-GraphMailboxPermission - {ex.Message}");
                if (!Helpers.IsTransient(ex) || attempts >= RetryCount) {
                    if (ex is GraphApiException gex)
                        WriteError(new ErrorRecord(gex, "GraphApiError", ErrorCategory.InvalidOperation, null));
                    else
                        WriteError(new ErrorRecord(ex, "GraphError", ErrorCategory.InvalidOperation, null));
                    return;
                }
                if (RetryDelayMilliseconds > 0) await Task.Delay(RetryDelayMilliseconds);
            }
            attempts++;
        } while (attempts <= RetryCount);
        if (lastException is not null)
            WriteError(new ErrorRecord(lastException, "GraphError", ErrorCategory.InvalidOperation, null));
    }

    private void ProcessMgGraph() {
        if (ParameterSetName == "Csv") {
            var psCsv = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
            psCsv.AddCommand("Import-Csv").AddParameter("Path", CsvPath);
            var rows = psCsv.Invoke();
            foreach (var row in rows.OfType<PSObject>()) {
                var id = row.Properties["PermissionId"].Value?.ToString();
                if (!string.IsNullOrWhiteSpace(id))
                    InvokeMgGraph(id);
            }
        } else {
            if (!ShouldProcess(PermissionId!, "Removing mailbox permission")) return;
            InvokeMgGraph(PermissionId!);
        }
    }

    private void InvokeMgGraph(string id) {
        var uri = $"https://graph.microsoft.com/v1.0/users/{UserPrincipalName}/permissions/{id}";
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "DELETE")
            .AddParameter("Uri", uri)
            .AddParameter("ContentType", "application/json");
        ps.Invoke();
    }
}
