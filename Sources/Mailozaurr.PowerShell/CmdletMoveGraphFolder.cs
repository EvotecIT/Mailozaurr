using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Mailozaurr;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Moves a Microsoft Graph mail folder.
/// </summary>
[Cmdlet(VerbsCommon.Move, "GraphFolder", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public class CmdletMoveGraphFolder : AsyncPSCmdlet {
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? FolderId { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? DestinationFolderId { get; set; }

    [Parameter(ParameterSetName = "Graph")]
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
                    WriteWarning("Move-GraphFolder - Connection not provided and no default session available.");
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
        if (!ShouldProcess(FolderId!, "Moving Graph folder")) {
            return;
        }
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                await MicrosoftGraphUtils.MoveFolderAsync(cred, UserPrincipalName!, FolderId!, DestinationFolderId!);
                return;
            } catch (Exception ex) {
                lastException = ex;
                WriteWarning($"Move-GraphFolder - {ex.Message}");
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
        if (!ShouldProcess(FolderId!, "Moving Graph folder")) {
            return;
        }
        var uri = $"https://graph.microsoft.com/v1.0/users/{UserPrincipalName}/mailFolders/{FolderId}/move";
        var body = JsonSerializer.Serialize(new { destinationId = DestinationFolderId });
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "POST")
            .AddParameter("Uri", uri)
            .AddParameter("Body", body)
            .AddParameter("ContentType", "application/json");
        ps.Invoke();
    }
}
