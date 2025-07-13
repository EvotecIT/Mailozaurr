using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Mailozaurr;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Retrieves mail messages for a user via Microsoft Graph.</para>
/// <para type="description">The <c>Get-EmailGraphMessage</c> cmdlet fetches messages for the specified user principal name using Microsoft Graph. It supports optional filters like subject, sender, recipient, priority and date range. Results can be limited and optionally deleted.</para>
/// </summary>
[Cmdlet(VerbsCommon.Get, "EmailGraphMessage")]
[OutputType(typeof(GraphMessageInfo))]
public sealed class CmdletGetEmailGraphMessage : AsyncPSCmdlet {
    /// <summary>
    /// User principal name whose mailbox is queried.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Graph")]
    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// Graph connection information.
    /// </summary>
    [Parameter(ParameterSetName = "Graph", ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    /// <summary>
    /// Message properties to select.
    /// </summary>
    [Parameter(ParameterSetName = "Graph")]
    [Parameter(ParameterSetName = "MgGraphRequest")]
    public string[]? Property { get; set; }

    /// <summary>
    /// <para type="description">Raw OData filter passed directly to Microsoft Graph.</para>
    /// </summary>
    [Parameter(ParameterSetName = "Graph")]
    [Parameter(ParameterSetName = "MgGraphRequest")]
    [Alias("ODataFilter")]
    public string? Filter { get; set; }

    /// <summary>
    /// Limits the number of returned messages.
    /// </summary>
    [Parameter(ParameterSetName = "Graph")]
    [Parameter(ParameterSetName = "MgGraphRequest")]
    public int? Limit { get; set; }

    /// <summary>
    /// Filters messages by subject text.
    /// </summary>
    [Parameter]
    public string? Subject { get; set; }

    /// <summary>
    /// Filters messages where the sender contains this value.
    /// </summary>
    [Parameter]
    public string? FromContains { get; set; }

    /// <summary>
    /// Filters messages where the recipient contains this value.
    /// </summary>
    [Parameter]
    public string? ToContains { get; set; }

    /// <summary>
    /// Filters messages by importance.
    /// </summary>
    [Parameter]
    public MessagePriority? Priority { get; set; }

    /// <summary>
    /// Retrieves messages received since this date.
    /// </summary>
    [Parameter]
    public DateTime? Since { get; set; }

    /// <summary>
    /// Retrieves messages received before this date.
    /// </summary>
    [Parameter]
    public DateTime? Before { get; set; }

    /// <summary>
    /// Filters messages that have attachments.
    /// </summary>
    [Parameter]
    public SwitchParameter HasAttachment { get; set; }

    /// <summary>
    /// When present, retrieves all messages ignoring limit.
    /// </summary>
    [Parameter]
    public SwitchParameter All { get; set; }

    /// <summary>
    /// Deletes messages after retrieval when set.
    /// </summary>
    [Parameter]
    public SwitchParameter Delete { get; set; }

    /// <summary>
    /// When specified, uses Invoke-MgGraphRequest for the operation.
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

    protected override Task ProcessRecordAsync() {
        if (ParameterSetName == "MgGraphRequest") {
            return ProcessMgGraph();
        }

        var conn = Connection ?? DefaultSessions.GraphSession;
        if (conn == null) {
            WriteWarning("Get-EmailGraphMessage - Connection not provided and no default session available.");
            return Task.CompletedTask;
        }

        return ProcessGraphAsync(conn.Credential);
    }

    private async Task ProcessGraphAsync(GraphCredential cred) {
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        MicrosoftGraphUtils.MaxConcurrentRequests = MaxConcurrentRequests;
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                var filter = BuildFilter(Filter);
                var messages = await MicrosoftGraphUtils.GetMailMessagesAsync(
                    cred,
                    UserPrincipalName!,
                    Property,
                    filter,
                    Limit);

                foreach (var msg in messages) {
                    var info = new GraphMessageInfo(msg, UserPrincipalName!);
                    WriteObject(info);
                    if (Delete.IsPresent && msg.TryGetValue("id", out var idObj) && idObj is string id) {
                        await MicrosoftGraphUtils.DeleteMailMessageAsync(cred, UserPrincipalName!, id);
                    }
                }
                return;
            } catch (Exception ex) {
                lastException = ex;
                WriteWarning($"Get-EmailGraphMessage - {ex.Message}");
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

    private Task ProcessMgGraph() {
        var filter = BuildFilter(Filter);
        var query = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(filter)) query["$filter"] = filter;
        if (Property != null && Property.Length > 0) query["$select"] = string.Join(",", Property);
        if (Limit.HasValue) query["$top"] = Limit.Value.ToString();
        var uri = MicrosoftGraphUtils.JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{UserPrincipalName}/messages", query);

        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        var parameters = new Hashtable { { "Method", "GET" }, { "Uri", uri } };
        ps.AddCommand("Invoke-MgGraphRequest");
        ps.AddParameters(parameters);
        var results = ps.Invoke();
        foreach (var res in results) {
            WriteObject(res);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Combines built-in conditions with a raw OData filter.
    /// </summary>
    private string BuildFilter(string? rawFilter) {
        var filters = new List<string>();
        if (!All.IsPresent) {
            if (!string.IsNullOrWhiteSpace(Subject)) {
                filters.Add($"contains(subject,'{Subject.Replace("'", "''")}')");
            }
            if (!string.IsNullOrWhiteSpace(FromContains)) {
                filters.Add($"contains(from/emailAddress/address,'{FromContains.Replace("'", "''")}')");
            }
            if (!string.IsNullOrWhiteSpace(ToContains)) {
                filters.Add($"toRecipients/any(r:contains(r/emailAddress/address,'{ToContains.Replace("'", "''")}'))");
            }
            if (Since.HasValue) {
                filters.Add($"receivedDateTime ge {Since.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}");
            }
            if (Before.HasValue) {
                filters.Add($"receivedDateTime le {Before.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}");
            }
        }
        if (HasAttachment.IsPresent) {
            filters.Add("hasAttachments eq true");
        }
        var filter = string.Join(" and ", filters);
        if (!string.IsNullOrWhiteSpace(rawFilter)) {
            filter = string.IsNullOrWhiteSpace(filter) ? rawFilter : $"{filter} and {rawFilter}";
        }
        return filter;
    }
}
