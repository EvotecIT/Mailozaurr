using System.Management.Automation;
using System.Threading.Tasks;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Creates a new calendar event via Microsoft Graph.
/// </summary>
[Cmdlet(VerbsCommon.New, "GraphEvent", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(GraphEvent))]
public sealed class CmdletNewGraphEvent : AsyncPSCmdlet
{
    /// <summary>
    /// User principal name owning the calendar.
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// Event object to create.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Event")]
    [ValidateNotNull]
    public GraphEvent? Event { get; set; }

    /// <summary>
    /// Builder used to construct the event.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Builder")]
    [ValidateNotNull]
    public GraphEventBuilder? EventBuilder { get; set; }

    /// <summary>
    /// Graph connection context for the request.
    /// </summary>
    [Parameter(ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    [Parameter]
    public int TimeoutSeconds { get; set; } = 100;

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
    /// Creates the event using Microsoft Graph.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override Task ProcessRecordAsync() {
        var conn = Connection ?? DefaultSessions.GraphSession;
        if (conn == null) {
            WriteWarning("New-GraphEvent - Connection not provided and no default session available.");
            return Task.CompletedTask;
        }
        return ProcessGraphAsync(conn.Credential);
    }

    private async Task ProcessGraphAsync(GraphCredential cred) {
        if (!ShouldProcess(UserPrincipalName!, "Creating Graph event")) {
            return;
        }
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        int attempts = 0;
        Exception? last = null;
        GraphEvent toCreate = ParameterSetName == "Builder" ? EventBuilder! : Event!;
        do {
            try {
                var result = await MicrosoftGraphUtils.NewEventAsync(cred, UserPrincipalName!, toCreate);
                WriteObject(result);
                return;
            } catch (Exception ex) {
                last = ex;
                WriteWarning($"New-GraphEvent - {ex.Message}");
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
