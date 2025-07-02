using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using System.Linq;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Clears the Junk Email folder via Microsoft Graph.
/// </summary>
[Cmdlet(VerbsCommon.Clear, "GraphJunk", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class CmdletClearGraphJunk : AsyncPSCmdlet {
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    [Parameter(ParameterSetName = "Graph", ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    [Parameter]
    public string[]? Property { get; set; }

    [Parameter]
    public SwitchParameter Preview { get; set; }

    [Parameter]
    public string[]? SkipId { get; set; }

    [Parameter]
    public string[]? SkipFrom { get; set; }

    [Parameter]
    public string[]? SkipTo { get; set; }

    [Parameter]
    public string[]? SkipSubjectContains { get; set; }

    [Parameter]
    public int TimeoutSeconds { get; set; } = 100;

    [Parameter]
    public int RetryCount { get; set; } = 0;

    [Parameter]
    public int RetryDelayMilliseconds { get; set; } = 0;

    protected override Task ProcessRecordAsync() {
        switch (ParameterSetName) {
            case "Graph":
                var conn = Connection ?? DefaultSessions.GraphSession;
                if (conn == null) {
                    WriteWarning("Clear-GraphJunk - Connection not provided and no default session available.");
                    return Task.CompletedTask;
                }
                if (Preview.IsPresent) {
                    return ProcessPreviewGraphAsync(conn.Credential);
                }
                return ProcessGraphAsync(conn.Credential);
            case "MgGraphRequest":
                if (Preview.IsPresent) {
                    ProcessPreviewMgGraph();
                } else {
                    ProcessMgGraph();
                }
                return Task.CompletedTask;
            default:
                return Task.CompletedTask;
        }
    }

    private async Task ProcessGraphAsync(GraphCredential cred) {
        if (!ShouldProcess(UserPrincipalName!, "Clearing Graph junk")) return;
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                await MicrosoftGraphUtils.ClearJunkMailAsync(
                    cred,
                    UserPrincipalName!,
                    SkipId,
                    SkipFrom,
                    SkipTo,
                    SkipSubjectContains);
                return;
            } catch (Exception ex) {
                lastException = ex;
                WriteWarning($"Clear-GraphJunk - {ex.Message}");
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

    private async Task ProcessPreviewGraphAsync(GraphCredential cred) {
        var props = new List<string>();
        if (Property != null && Property.Length > 0) props.AddRange(Property);
        if (!props.Contains("id")) props.Add("id");
        if (!props.Contains("bodyPreview")) props.Add("bodyPreview");
        if (SkipFrom != null && !props.Contains("from")) props.Add("from");
        if (SkipTo != null && !props.Contains("toRecipients")) props.Add("toRecipients");
        if (SkipSubjectContains != null && !props.Contains("subject")) props.Add("subject");

        var messages = await MicrosoftGraphUtils.GetJunkMailMessagesAsync(
            cred,
            UserPrincipalName!,
            props,
            SkipId,
            SkipFrom,
            SkipTo,
            SkipSubjectContains);

        foreach (var msg in messages) {
            WriteObject(PSObject.AsPSObject(msg));
        }
    }

    private void ProcessMgGraph() {
        if (!ShouldProcess(UserPrincipalName!, "Clearing Graph junk")) return;
        var select = new List<string> { "id" };
        if (SkipSubjectContains != null) select.Add("subject");
        if (SkipFrom != null) select.Add("from");
        if (SkipTo != null) select.Add("toRecipients");
        var listUri = $"https://graph.microsoft.com/v1.0/users/{UserPrincipalName}/mailFolders/junkemail/messages?$select=" + string.Join(",", select);
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "GET")
            .AddParameter("Uri", listUri);
        var results = ps.Invoke();
        var dict = results
            .OfType<PSObject>()
            .Select(o => o.Properties.ToDictionary(p => p.Name, p => p.Value))
            .ToList();
        var filtered = MicrosoftGraphUtils.FilterJunkMessages(dict, SkipId, SkipFrom, SkipTo, SkipSubjectContains);
        foreach (var msg in filtered) {
            var id = msg["id"] as string;
            if (string.IsNullOrWhiteSpace(id)) continue;
            var delPs = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
            delPs.AddCommand("Invoke-MgGraphRequest")
                .AddParameter("Method", "DELETE")
                .AddParameter("Uri", $"https://graph.microsoft.com/v1.0/users/{UserPrincipalName}/messages/{id}")
                .AddParameter("ContentType", "application/json");
            delPs.Invoke();
        }
    }

    private void ProcessPreviewMgGraph() {
        var props = new List<string>();
        if (Property != null && Property.Length > 0) props.AddRange(Property);
        if (!props.Contains("id")) props.Add("id");
        if (SkipFrom != null && !props.Contains("from")) props.Add("from");
        if (SkipTo != null && !props.Contains("toRecipients")) props.Add("toRecipients");
        if (SkipSubjectContains != null && !props.Contains("subject")) props.Add("subject");
        var listUri = $"https://graph.microsoft.com/v1.0/users/{UserPrincipalName}/mailFolders/junkemail/messages?$select=" + string.Join(",", props);
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "GET")
            .AddParameter("Uri", listUri);
        var results = ps.Invoke();
        var dict = results
            .OfType<PSObject>()
            .Select(o => o.Properties.ToDictionary(p => p.Name, p => p.Value))
            .ToList();
        var filtered = MicrosoftGraphUtils.FilterJunkMessages(dict, SkipId, SkipFrom, SkipTo, SkipSubjectContains);
        foreach (var msg in filtered) {
            WriteObject(PSObject.AsPSObject(msg));
        }
    }
}
