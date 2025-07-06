using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Mailozaurr;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Moves a Microsoft Graph message to a different folder.
/// </summary>
[Cmdlet(VerbsCommon.Move, "GraphMessage", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public class CmdletMoveGraphMessage : AsyncPSCmdlet {
    /// <summary>
    /// UPN of the mailbox owner containing the message.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// Identifier of the message to move.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? MessageId { get; set; }

    /// <summary>
    /// Target folder identifier where the message will be moved.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? DestinationFolderId { get; set; }

    /// <summary>
    /// Optional Graph connection used when moving the message.
    /// </summary>
    [Parameter(ParameterSetName = "Graph")]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }


    /// <summary>
    /// Indicates that the message should be moved using Invoke-MgGraphRequest.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    [Parameter]
    public int TimeoutSeconds { get; set; } = 100;

    [Parameter]
    public int RetryCount { get; set; } = 0;

    [Parameter]
    public int RetryDelayMilliseconds { get; set; } = 0;

    /// <summary>
    /// Processes the cmdlet invocation.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override Task ProcessRecordAsync() {
        switch (ParameterSetName) {
            case "Graph":
                var conn = Connection ?? DefaultSessions.GraphSession;
                if (conn == null) {
                    WriteWarning("Move-GraphMessage - Connection not provided and no default session available.");
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

    /// <summary>
    /// Executes the move operation using the Graph SDK.
    /// </summary>
    /// <param name="cred">Credential used to access Graph.</param>
    private async Task ProcessGraphAsync(GraphCredential cred) {
        if (!ShouldProcess(MessageId!, "Moving Graph message")) {
            return;
        }
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                await MicrosoftGraphUtils.MoveMailMessageAsync(cred, UserPrincipalName!, MessageId!, DestinationFolderId!);
                return;
            } catch (Exception ex) {
                lastException = ex;
                WriteWarning($"Move-GraphMessage - {ex.Message}");
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

    /// <summary>
    /// Executes the move operation using the <c>Invoke-MgGraphRequest</c> cmdlet.
    /// </summary>
    private void ProcessMgGraph() {
        if (!ShouldProcess(MessageId!, "Moving Graph message")) {
            return;
        }
        var uri = $"https://graph.microsoft.com/v1.0/users/{UserPrincipalName}/messages/{MessageId}/move";
        var body = System.Text.Json.JsonSerializer.Serialize(new { destinationId = DestinationFolderId });
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "POST")
            .AddParameter("Uri", uri)
            .AddParameter("Body", body)
            .AddParameter("ContentType", "application/json");
        ps.Invoke();
    }
}
