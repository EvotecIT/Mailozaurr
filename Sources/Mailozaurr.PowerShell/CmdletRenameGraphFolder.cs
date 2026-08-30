using Mailozaurr;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Renames a Microsoft Graph mail folder.
/// </summary>
[Cmdlet("Rename", "GraphFolder", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public class CmdletRenameGraphFolder : AsyncPSCmdlet {
    /// <summary>User principal name owning the folder.</summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    /// <summary>Identifier of the folder to rename.</summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? FolderId { get; set; }

    /// <summary>The new folder name.</summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? NewName { get; set; }

    /// <summary>Connection information for Microsoft Graph.</summary>
    [Parameter(ParameterSetName = "Graph")]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    /// <summary>Use <c>Invoke-MgGraphRequest</c> instead of built-in logic.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    /// <summary>Request timeout in seconds.</summary>
    [Parameter]
    public int TimeoutSeconds { get; set; } = 100;

    /// <summary>
    /// Maximum number of concurrent requests during rename.
    /// </summary>
    [Parameter]
    public int MaxConcurrentRequests { get; set; } = 5;

    /// <summary>Number of retries on transient errors.</summary>
    [Parameter]
    public int RetryCount { get; set; } = 0;

    /// <summary>Delay between retries in milliseconds.</summary>
    [Parameter]
    public int RetryDelayMilliseconds { get; set; } = 0;

    /// <summary>
    /// Renames a folder using Microsoft Graph.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override Task ProcessRecordAsync() {
        switch (ParameterSetName) {
            case "Graph":
                var conn = Connection ?? DefaultSessions.GraphSession;
                if (conn == null) {
                    WriteWarning("Rename-GraphFolder - Connection not provided and no default session available.");
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
        var dryRun = !ShouldProcess(FolderId!, "Renaming Graph folder");
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        MicrosoftGraphUtils.MaxConcurrentRequests = MaxConcurrentRequests;
        if (dryRun) {
            await MicrosoftGraphUtils.RenameFolderAsync(cred, UserPrincipalName!, FolderId!, NewName!, dryRun: true);
            return;
        }
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                await MicrosoftGraphUtils.RenameFolderAsync(cred, UserPrincipalName!, FolderId!, NewName!, dryRun: false);
                return;
            } catch (Exception ex) {
                lastException = ex;
                WriteWarning($"Rename-GraphFolder - {ex.Message}");
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
        if (!ShouldProcess(FolderId!, "Renaming Graph folder")) {
            return;
        }
        var uri = MicrosoftGraphUtils.BuildGraphUri(
            GraphEndpoint.V1,
            MicrosoftGraphUtils.BuildGraphPath("users", UserPrincipalName!, "mailFolders", FolderId!));
        var body = JsonSerializer.Serialize(
            new GraphFolderRenameRequest { DisplayName = NewName },
            GraphJsonContext.Default.GraphFolderRenameRequest);
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "PATCH")
            .AddParameter("Uri", uri)
            .AddParameter("Body", body)
            .AddParameter("ContentType", "application/json");
        ps.Invoke();
    }
}
