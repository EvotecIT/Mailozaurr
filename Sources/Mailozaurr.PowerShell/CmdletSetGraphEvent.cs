using System.Management.Automation;
using System.Threading.Tasks;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Updates an existing calendar event via Microsoft Graph.
/// </summary>
[Cmdlet(VerbsCommon.Set, "GraphEvent", SupportsShouldProcess = true)]
[OutputType(typeof(GraphEvent))]
public sealed class CmdletSetGraphEvent : AsyncPSCmdlet {
    [Parameter(Mandatory = true)]
    public string? UserPrincipalName { get; set; }

    [Parameter(Mandatory = true)]
    public string? EventId { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNull]
    public GraphEvent? Event { get; set; }

    [Parameter(ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    [Parameter]
    public int TimeoutSeconds { get; set; } = 100;

    [Parameter]
    public int RetryCount { get; set; } = 0;

    [Parameter]
    public int RetryDelayMilliseconds { get; set; } = 0;

    protected override Task ProcessRecordAsync() {
        var conn = Connection ?? DefaultSessions.GraphSession;
        if (conn == null) {
            WriteWarning("Set-GraphEvent - Connection not provided and no default session available.");
            return Task.CompletedTask;
        }
        return ProcessGraphAsync(conn.Credential);
    }

    private async Task ProcessGraphAsync(GraphCredential cred) {
        if (!ShouldProcess(EventId!, "Updating event")) {
            return;
        }
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        int attempts = 0;
        Exception? last = null;
        do {
            try {
                var result = await MicrosoftGraphUtils.UpdateEventAsync(cred, UserPrincipalName!, EventId!, Event!);
                WriteObject(result);
                return;
            } catch (Exception ex) {
                last = ex;
                WriteWarning($"Set-GraphEvent - {ex.Message}");
                if (!Helpers.IsTransient(ex) || attempts >= RetryCount) {
                    if (ex is GraphApiException gex) WriteError(new ErrorRecord(gex, "GraphApiError", ErrorCategory.InvalidOperation, null));
                    else WriteError(new ErrorRecord(ex, "GraphError", ErrorCategory.InvalidOperation, null));
                    return;
                }
                if (RetryDelayMilliseconds > 0) await Task.Delay(RetryDelayMilliseconds);
            }
            attempts++;
        } while (attempts <= RetryCount);
        if (last != null) WriteError(new ErrorRecord(last, "GraphError", ErrorCategory.InvalidOperation, null));
    }
}
