using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Retrieves mail messages for a user via Microsoft Graph.</para>
/// <para type="description">The <c>Get-EmailGraphMessage</c> cmdlet fetches messages for the specified user principal name using Microsoft Graph. It supports optional filters like subject, sender, recipient, priority and date range. Results can be limited and optionally deleted.</para>
/// </summary>
[Cmdlet(VerbsCommon.Get, "EmailGraphMessage")]
[OutputType(typeof(object))]
public sealed class CmdletGetEmailGraphMessage : AsyncPSCmdlet {
    [Parameter(Mandatory = true, ParameterSetName = "Graph")]
    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    [Parameter(ParameterSetName = "Graph", ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    [Parameter(ParameterSetName = "Graph")]
    [Parameter(ParameterSetName = "MgGraphRequest")]
    public string[]? Property { get; set; }

    [Parameter(ParameterSetName = "Graph")]
    [Parameter(ParameterSetName = "MgGraphRequest")]
    public string? Filter { get; set; }

    [Parameter(ParameterSetName = "Graph")]
    [Parameter(ParameterSetName = "MgGraphRequest")]
    public int? Limit { get; set; }

    [Parameter]
    public string? Subject { get; set; }

    [Parameter]
    public string? FromContains { get; set; }

    [Parameter]
    public string? ToContains { get; set; }

    [Parameter]
    public MessagePriority? Priority { get; set; }

    [Parameter]
    public DateTime? Since { get; set; }

    [Parameter]
    public DateTime? Before { get; set; }

    [Parameter]
    public SwitchParameter HasAttachment { get; set; }

    [Parameter]
    public SwitchParameter All { get; set; }

    [Parameter]
    public SwitchParameter Delete { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

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
        var filter = BuildFilter();
        var messages = await MicrosoftGraphUtils.GetMailMessagesAsync(
            cred,
            UserPrincipalName!,
            Property,
            filter,
            Limit);

        foreach (var msg in messages) {
            WriteObject(PSObject.AsPSObject(msg));
            if (Delete.IsPresent && msg.TryGetValue("id", out var idObj) && idObj is string id) {
                await MicrosoftGraphUtils.DeleteMailMessageAsync(cred, UserPrincipalName!, id);
            }
        }
    }

    private Task ProcessMgGraph() {
        var filter = BuildFilter();
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

    private string BuildFilter() {
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
        if (!string.IsNullOrWhiteSpace(Filter)) {
            filter = string.IsNullOrWhiteSpace(filter) ? Filter : $"{filter} and {Filter}";
        }
        return filter;
    }
}
