using Mailozaurr;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Moves a Microsoft Graph mail folder.
/// </summary>
[Cmdlet(VerbsCommon.Move, "GraphFolder", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public class CmdletMoveGraphFolder : AsyncPSCmdlet {
    private const string ParentParameterSet = "Parent";
    private const string RootParameterSet = "Root";
    /// <summary>User principal name owning the folder.</summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    /// <summary>Identifier of the folder to move.</summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? FolderId { get; set; }

    /// <summary>Identifier of the destination folder.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParentParameterSet)]
    [ValidateNotNullOrEmpty]
    public string? DestinationFolderId { get; set; }

    /// <summary>Move folder to the root.</summary>
    [Parameter(ParameterSetName = RootParameterSet)]
    public SwitchParameter Root { get; set; }

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
    /// Maximum number of concurrent requests during the move operation.
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
    /// Moves a folder within a mailbox using Microsoft Graph.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override Task ProcessRecordAsync() {
        switch (ParameterSetName) {
            case ParentParameterSet:
            case RootParameterSet:
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
        var dryRun = !ShouldProcess(FolderId!, "Moving Graph folder");
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        MicrosoftGraphUtils.MaxConcurrentRequests = MaxConcurrentRequests;
        var dest = ParameterSetName == RootParameterSet ? "msgfolderroot" : DestinationFolderId!;
        if (dryRun) {
            await MicrosoftGraphUtils.MoveFolderAsync(cred, UserPrincipalName!, FolderId!, dest, dryRun: true);
            return;
        }
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                await MicrosoftGraphUtils.MoveFolderAsync(cred, UserPrincipalName!, FolderId!, dest, dryRun: false);
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
        var dest = ParameterSetName == RootParameterSet ? "msgfolderroot" : DestinationFolderId;
        var uri = MicrosoftGraphUtils.BuildGraphUri(
            GraphEndpoint.V1,
            $"/users/{UserPrincipalName}/mailFolders/{FolderId}/move");
        var body = JsonSerializer.Serialize(new GraphDestinationRequest { DestinationId = dest }, MailozaurrJsonContext.Default.GraphDestinationRequest);
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "POST")
            .AddParameter("Uri", uri)
            .AddParameter("Body", body)
            .AddParameter("ContentType", "application/json");
        ps.Invoke();
    }
}