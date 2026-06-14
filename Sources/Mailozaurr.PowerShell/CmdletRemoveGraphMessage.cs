using Mailozaurr;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Deletes a message from Microsoft Graph.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "GraphMessage", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public class CmdletRemoveGraphMessage : AsyncPSCmdlet {
    /// <summary>User principal name owning the message.</summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    /// <summary>Identifier of the message to delete.</summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? MessageId { get; set; }

    /// <summary>Connection used for Graph operations.</summary>
    [Parameter(ParameterSetName = "Graph")]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    /// <summary>Indicates using Invoke-MgGraphRequest.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    [Parameter]
    public int TimeoutSeconds { get; set; } = 100;

    /// <summary>
    /// Maximum number of concurrent Graph requests.
    /// </summary>
    [Parameter]
    public int MaxConcurrentRequests { get; set; } = 5;

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

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        switch (ParameterSetName) {
            case "Graph":
                var conn = Connection ?? DefaultSessions.GraphSession;
                if (conn == null) {
                    WriteWarning("Remove-GraphMessage - Connection not provided and no default session available.");
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
        var dryRun = !ShouldProcess(MessageId!, "Deleting message via Graph");
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        MicrosoftGraphUtils.MaxConcurrentRequests = MaxConcurrentRequests;
        if (dryRun) {
            await MicrosoftGraphUtils.DeleteMailMessageAsync(cred, UserPrincipalName!, MessageId!, dryRun: true);
            return;
        }
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                await MicrosoftGraphUtils.DeleteMailMessageAsync(cred, UserPrincipalName!, MessageId!, dryRun: false);
                return;
            } catch (Exception ex) {
                lastException = ex;
                WriteWarning($"Remove-GraphMessage - {ex.Message}");
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
        if (!ShouldProcess(MessageId!, "Deleting message via Graph")) {
            return;
        }
        var uri = MicrosoftGraphUtils.BuildGraphUri(
            GraphEndpoint.V1,
            $"/users/{UserPrincipalName}/messages/{MessageId}");
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "DELETE")
            .AddParameter("Uri", uri)
            .AddParameter("ContentType", "application/json");
        ps.Invoke();
    }
}