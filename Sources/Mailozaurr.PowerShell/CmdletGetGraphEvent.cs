using Mailozaurr;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Retrieves calendar events using Microsoft Graph.
/// </summary>
[Cmdlet(VerbsCommon.Get, "GraphEvent")]
[OutputType(typeof(Dictionary<string, object>))]
public sealed class CmdletGetGraphEvent : AsyncPSCmdlet {
    /// <summary>
    /// User principal name whose calendar events are retrieved.
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// Graph connection information to use for the request.
    /// </summary>
    [Parameter(ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    /// <summary>
    /// Additional event properties to select.
    /// </summary>
    [Parameter]
    public string[]? Property { get; set; }

    /// <summary>
    /// OData filter string applied to the query.
    /// </summary>
    [Parameter]
    public string? Filter { get; set; }

    /// <summary>
    /// Maximum number of events to return.
    /// </summary>
    [Parameter]
    public int? Limit { get; set; }

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    [Parameter]
    public int TimeoutSeconds { get; set; } = 100;

    /// <summary>
    /// Number of retry attempts on transient errors.
    /// </summary>
    [Parameter]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Delay between retry attempts in milliseconds.
    /// </summary>
    [Parameter]
    public int RetryDelayMilliseconds { get; set; } = 0;

    /// <summary>
    /// Retrieves events for the specified user.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override Task ProcessRecordAsync() {
        var conn = Connection ?? DefaultSessions.GraphSession;
        if (conn == null) {
            WriteWarning("Get-GraphEvent - Connection not provided and no default session available.");
            return Task.CompletedTask;
        }
        return ProcessGraphAsync(conn.Credential);
    }

    private async Task ProcessGraphAsync(GraphCredential cred) {
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        int attempts = 0;
        Exception? last = null;
        do {
            try {
                var events = await MicrosoftGraphUtils.GetEventsAsync(cred, UserPrincipalName!, Property, Filter, Limit);
                foreach (var ev in events) WriteObject(ev);
                return;
            } catch (Exception ex) {
                last = ex;
                WriteWarning($"Get-GraphEvent - {ex.Message}");
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