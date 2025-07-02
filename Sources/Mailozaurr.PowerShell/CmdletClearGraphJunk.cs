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
        if (SkipSubjectContains != null && !props.Contains("subject")) props.Add("subject");
        if (SkipFrom != null && !props.Contains("from")) props.Add("from");
        if (SkipTo != null && !props.Contains("toRecipients")) props.Add("toRecipients");
        if (!props.Contains("bodyPreview")) props.Add("bodyPreview");

        var messages = await MicrosoftGraphUtils.GetJunkMailMessagesAsync(cred, UserPrincipalName!, props);
        foreach (var msg in messages) {
            var id = msg["id"] as string;
            if (SkipId != null && id != null && SkipId.Contains(id, StringComparer.OrdinalIgnoreCase)) continue;
            if (SkipFrom != null && msg.TryGetValue("from", out var fromObj) &&
                fromObj is Dictionary<string, object> fDict &&
                fDict.TryGetValue("emailAddress", out var addrObj) &&
                addrObj is Dictionary<string, object> addr &&
                addr.TryGetValue("address", out var fromAddrObj) &&
                fromAddrObj is string fromAddr &&
                SkipFrom.Contains(fromAddr, StringComparer.OrdinalIgnoreCase)) {
                continue;
            }
            if (SkipTo != null && msg.TryGetValue("toRecipients", out var toObj) &&
                toObj is object[] arr &&
                arr.OfType<Dictionary<string, object>>().Any(rec =>
                    rec.TryGetValue("emailAddress", out var tAddrObj) &&
                    tAddrObj is Dictionary<string, object> tAddr &&
                    tAddr.TryGetValue("address", out var addrVal) &&
                    addrVal is string addrStr &&
                    SkipTo.Contains(addrStr, StringComparer.OrdinalIgnoreCase))) {
                continue;
            }
            if (SkipSubjectContains != null && msg.TryGetValue("subject", out var subjObj) &&
                subjObj is string subj &&
                SkipSubjectContains.Any(s => subj.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)) {
                continue;
            }

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
        foreach (var res in results) {
            var obj = PSObject.AsPSObject(res);
            var id = obj.Properties["id"]?.Value as string;
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (SkipId != null && SkipId.Contains(id, StringComparer.OrdinalIgnoreCase)) continue;
            if (SkipFrom != null && obj.Properties["from"]?.Value is PSObject fp &&
                fp.Properties["emailAddress"]?.Value is PSObject ea &&
                ea.Properties["address"]?.Value is string addr &&
                SkipFrom.Contains(addr, StringComparer.OrdinalIgnoreCase)) continue;
            if (SkipTo != null && obj.Properties["toRecipients"]?.Value is object[] recArr &&
                recArr.OfType<PSObject>().Any(rec => rec.Properties["emailAddress"]?.Value is PSObject ea2 &&
                    ea2.Properties["address"]?.Value is string tAddr &&
                    SkipTo.Contains(tAddr, StringComparer.OrdinalIgnoreCase))) continue;
            if (SkipSubjectContains != null && obj.Properties["subject"]?.Value is string subj &&
                SkipSubjectContains.Any(s => subj.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)) continue;

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
        foreach (var res in results) {
            var obj = PSObject.AsPSObject(res);
            var id = obj.Properties["id"]?.Value as string;
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (SkipId != null && SkipId.Contains(id, StringComparer.OrdinalIgnoreCase)) continue;
            if (SkipFrom != null && obj.Properties["from"]?.Value is PSObject fp &&
                fp.Properties["emailAddress"]?.Value is PSObject ea &&
                ea.Properties["address"]?.Value is string addr &&
                SkipFrom.Contains(addr, StringComparer.OrdinalIgnoreCase)) continue;
            if (SkipTo != null && obj.Properties["toRecipients"]?.Value is object[] recArr &&
                recArr.OfType<PSObject>().Any(rec => rec.Properties["emailAddress"]?.Value is PSObject ea2 &&
                    ea2.Properties["address"]?.Value is string tAddr &&
                    SkipTo.Contains(tAddr, StringComparer.OrdinalIgnoreCase))) continue;
            if (SkipSubjectContains != null && obj.Properties["subject"]?.Value is string subj &&
                SkipSubjectContains.Any(s => subj.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)) continue;

            WriteObject(obj);
        }
    }
}
