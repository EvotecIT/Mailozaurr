using Mailozaurr;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Updates properties of an existing Microsoft Graph message.
/// </summary>
[Cmdlet(VerbsCommon.Set, "GraphMessage", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public class CmdletSetGraphMessage : AsyncPSCmdlet {
    /// <summary>
    /// UPN of the mailbox owner containing the message.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// Identifier of the message to modify.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? MessageId { get; set; }

    /// <summary>
    /// Marks the message as read when set.
    /// </summary>
    [Parameter]
    public SwitchParameter Read { get; set; }

    /// <summary>
    /// Optional Graph connection used when updating the message.
    /// </summary>
    [Parameter(ParameterSetName = "Graph")]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }


    /// <summary>
    /// Indicates that the message should be updated using Invoke-MgGraphRequest.
    /// </summary>
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

    /// <summary>
    /// Processes the cmdlet invocation.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override Task ProcessRecordAsync() {
        switch (ParameterSetName) {
            case "Graph":
                var conn = Connection ?? DefaultSessions.GraphSession;
                if (conn == null) {
                    WriteWarning("Set-GraphMessage - Connection not provided and no default session available.");
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
    /// Executes the update operation using the Graph SDK.
    /// </summary>
    /// <param name="cred">Credential used to access Graph.</param>
    private async Task ProcessGraphAsync(GraphCredential cred) {
        var dryRun = !ShouldProcess(MessageId!, "Updating message via Graph");
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        MicrosoftGraphUtils.MaxConcurrentRequests = MaxConcurrentRequests;
        if (dryRun) {
            await MicrosoftGraphUtils.SetMailMessageAsync(cred, UserPrincipalName!, MessageId!, Read.IsPresent, dryRun: true);
            return;
        }
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                await MicrosoftGraphUtils.SetMailMessageAsync(cred, UserPrincipalName!, MessageId!, Read.IsPresent, dryRun: false);
                return;
            } catch (Exception ex) {
                lastException = ex;
                WriteWarning($"Set-GraphMessage - {ex.Message}");
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
    /// Executes the update operation using the <c>Invoke-MgGraphRequest</c> cmdlet.
    /// </summary>
    private void ProcessMgGraph() {
        if (!ShouldProcess(MessageId!, "Updating message via Graph")) {
            return;
        }
        var uri = MicrosoftGraphUtils.BuildGraphUri(
            GraphEndpoint.V1,
            MicrosoftGraphUtils.BuildGraphPath("users", UserPrincipalName!, "messages", MessageId!));
        var body = JsonSerializer.Serialize(new GraphMarkReadRequest { IsRead = Read.IsPresent }, GraphJsonContext.Default.GraphMarkReadRequest);
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "PATCH")
            .AddParameter("Uri", uri)
            .AddParameter("Body", body)
            .AddParameter("ContentType", "application/json");
        ps.Invoke();
    }
}
