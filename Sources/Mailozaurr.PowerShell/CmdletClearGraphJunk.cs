using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Clears the Junk Email folder via Microsoft Graph.
/// </summary>
[Cmdlet(VerbsCommon.Clear, "GraphJunk", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class CmdletClearGraphJunk : AsyncPSCmdlet {
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

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
        switch (ParameterSetName) {
            case "Graph":
                var conn = Connection ?? DefaultSessions.GraphSession;
                if (conn == null) {
                    WriteWarning("Clear-GraphJunk - Connection not provided and no default session available.");
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
        if (!ShouldProcess(UserPrincipalName!, "Clearing Graph junk")) return;
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                await MicrosoftGraphUtils.ClearJunkMailAsync(cred, UserPrincipalName!);
                return;
            } catch (Exception ex) {
                lastException = ex;
                WriteWarning($"Clear-GraphJunk - {ex.Message}");
                if (!Helpers.IsTransient(ex) || attempts >= RetryCount) {
                    if (ex is GraphApiException gex) {
                        WriteError(new ErrorRecord(gex, "GraphApiError", ErrorCategory.InvalidOperation, null));
                    } else {
                        WriteError(new ErrorRecord(ex, "GraphError", ErrorCategory.InvalidOperation, null));
                    }
                    return;
                }
                if (RetryDelayMilliseconds > 0) {
                    await Task.Delay(RetryDelayMilliseconds);
                }
            }
            attempts++;
        } while (attempts <= RetryCount);
        if (lastException is not null) {
            WriteError(new ErrorRecord(lastException, "GraphError", ErrorCategory.InvalidOperation, null));
        }
    }

    private void ProcessMgGraph() {
        if (!ShouldProcess(UserPrincipalName!, "Clearing Graph junk")) return;
        var listUri = $"https://graph.microsoft.com/v1.0/users/{UserPrincipalName}/mailFolders/junkemail/messages?$select=id";
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "GET")
            .AddParameter("Uri", listUri);
        var results = ps.Invoke();
        var ids = new List<string>();
        foreach (var res in results) {
            var obj = PSObject.AsPSObject(res);
            var id = obj.Properties["id"]?.Value as string;
            if (!string.IsNullOrWhiteSpace(id)) {
                ids.Add(id);
            }
        }
        foreach (var id in ids) {
            var delPs = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
            delPs.AddCommand("Invoke-MgGraphRequest")
                .AddParameter("Method", "DELETE")
                .AddParameter("Uri", $"https://graph.microsoft.com/v1.0/users/{UserPrincipalName}/messages/{id}")
                .AddParameter("ContentType", "application/json");
            delPs.Invoke();
        }
    }
}
