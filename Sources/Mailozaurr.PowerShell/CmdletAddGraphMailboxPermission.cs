using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Adds mailbox permissions via Microsoft Graph.
/// </summary>
[Cmdlet(VerbsCommon.Add, "GraphMailboxPermission", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public class CmdletAddGraphMailboxPermission : AsyncPSCmdlet {
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    [Parameter(ParameterSetName = "Graph")]
    [ValidateNotNullOrEmpty]
    public Hashtable? Permission { get; set; }

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
            WriteWarning("Add-GraphMailboxPermission - Connection not provided and no default session available.");
            return Task.CompletedTask;
        }

        if (ParameterSetName == "Csv") {
            return ProcessCsvAsync(conn.Credential);
        }

        return ProcessGraphAsync(conn.Credential, Permission!);
    }

    private async Task ProcessCsvAsync(GraphCredential cred) {
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Import-Csv").AddParameter("Path", CsvPath);
        var rows = ps.Invoke();
        foreach (var row in rows.OfType<PSObject>()) {
            var dict = row.Properties.ToDictionary(p => p.Name, p => p.Value);
            await ProcessGraphAsync(cred, new Hashtable(dict)).ConfigureAwait(false);
        }
    }

    private async Task ProcessGraphAsync(GraphCredential cred, Hashtable permission) {
        if (!ShouldProcess(UserPrincipalName!, "Adding mailbox permission")) return;
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        int attempts = 0;
        Exception? lastException = null;
        var body = JsonSerializer.Serialize(permission.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => e.Value));
        do {
            try {
                await MicrosoftGraphUtils.AddMailboxPermissionAsync(cred, UserPrincipalName!, body);
                return;
            } catch (Exception ex) {
                lastException = ex;
                WriteWarning($"Add-GraphMailboxPermission - {ex.Message}");
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
        if (!ShouldProcess(UserPrincipalName!, "Adding mailbox permission")) return;
        string body;
        if (ParameterSetName == "Csv") {
            var psCsv = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
            psCsv.AddCommand("Import-Csv").AddParameter("Path", CsvPath);
            var rows = psCsv.Invoke();
            foreach (var row in rows.OfType<PSObject>()) {
                var dict = row.Properties.ToDictionary(p => p.Name, p => p.Value);
                body = JsonSerializer.Serialize(dict);
                InvokeMgGraph(body);
            }
        } else {
            var conv = Permission!.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => e.Value);
            body = JsonSerializer.Serialize(conv);
            InvokeMgGraph(body);
        }
    }

    private void InvokeMgGraph(string body) {
        var uri = $"https://graph.microsoft.com/v1.0/users/{UserPrincipalName}/permissions";
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "POST")
            .AddParameter("Uri", uri)
            .AddParameter("Body", body)
            .AddParameter("ContentType", "application/json");
        ps.Invoke();
    }
}
