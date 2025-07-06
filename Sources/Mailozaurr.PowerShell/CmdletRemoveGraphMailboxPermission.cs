using System;
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
    public string[]? PermissionId { get; set; }

    [Parameter(ParameterSetName = "Object", ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphMailboxPermission[]? MailboxPermission { get; set; }

    [Parameter(ParameterSetName = "Filter")]
    public GraphMailboxRole[]? Role { get; set; }

    [Parameter(ParameterSetName = "Filter")]
    public string[]? GrantedToUser { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Csv")]
    [ValidateNotNullOrEmpty]
    public string? CsvPath { get; set; }

    [Parameter(ParameterSetName = "Graph", ValueFromPipeline = true)]
    [Parameter(ParameterSetName = "Object", ValueFromPipeline = true)]
    [Parameter(ParameterSetName = "Csv", ValueFromPipeline = true)]
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

    protected override async Task ProcessRecordAsync() {
        if (ParameterSetName == "MgGraphRequest") {
            ProcessMgGraph();
            return;
        }

        var conn = Connection ?? DefaultSessions.GraphSession;
        if (conn == null) {
            WriteWarning("Remove-GraphMailboxPermission - Connection not provided and no default session available.");
            return;
        }

        if (ParameterSetName == "Csv") {
            await ProcessCsvAsync(conn.Credential).ConfigureAwait(false);
            return;
        }

        if (ParameterSetName == "Object") {
            foreach (var perm in MailboxPermission!) {
                if (perm.UserPrincipalName == null)
                    perm.UserPrincipalName = UserPrincipalName;
                await ProcessGraphAsync(conn.Credential, perm).ConfigureAwait(false);
            }
            return;
        }

        if (ParameterSetName == "Filter") {
            await ProcessFilterAsync(conn.Credential).ConfigureAwait(false);
            return;
        }

        foreach (var id in PermissionId!)
            await ProcessGraphAsync(conn.Credential, id).ConfigureAwait(false);
        return;
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

    private async Task ProcessFilterAsync(GraphCredential cred) {
        var perms = await MicrosoftGraphUtils.GetMailboxPermissionsAsync(cred, UserPrincipalName!).ConfigureAwait(false);
        var filtered = perms.AsEnumerable();
        if (Role != null && Role.Length > 0)
            filtered = filtered.Where(p => p.Roles != null && p.Roles.Intersect(Role).Any());
        if (GrantedToUser != null && GrantedToUser.Length > 0)
            filtered = filtered.Where(p => p.GrantedTo?.User != null && GrantedToUser.Contains(p.GrantedTo.User, StringComparer.OrdinalIgnoreCase));
        foreach (var p in filtered)
            await ProcessGraphAsync(cred, p).ConfigureAwait(false);
    }

    private async Task ProcessGraphAsync(GraphCredential cred, object permission) {
        var id = permission switch {
            GraphMailboxPermission p => p.Id,
            string s => s,
            _ => null
        };
        if (id is null) return;
        if (!ShouldProcess(id, "Removing mailbox permission")) return;
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                await MicrosoftGraphUtils.RemoveMailboxPermissionAsync(cred, UserPrincipalName!, id);
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
        if (CsvPath != null) {
            var psCsv = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
            psCsv.AddCommand("Import-Csv").AddParameter("Path", CsvPath);
            var rows = psCsv.Invoke();
            foreach (var row in rows.OfType<PSObject>()) {
                var id = row.Properties["PermissionId"].Value?.ToString();
                if (!string.IsNullOrWhiteSpace(id))
                    InvokeMgGraph(id);
            }
        } else if (MailboxPermission != null) {
            foreach (var perm in MailboxPermission) {
                if (perm.Id != null) {
                    if (!ShouldProcess(perm.Id, "Removing mailbox permission")) continue;
                    InvokeMgGraph(perm.Id);
                }
            }
        } else if (PermissionId != null) {
            foreach (var id in PermissionId) {
                if (!ShouldProcess(id, "Removing mailbox permission")) continue;
                InvokeMgGraph(id);
            }
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
