using Mailozaurr;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Clears the Junk Email folder via Microsoft Graph.
/// </summary>
[Cmdlet(VerbsCommon.Clear, "GraphJunk", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class CmdletClearGraphJunk : AsyncPSCmdlet {
    /// <summary>
    /// User principal name of the mailbox to clean.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// Connection information for Microsoft Graph.
    /// </summary>
    [Parameter(ParameterSetName = "Graph", ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    /// <summary>
    /// Returns the Microsoft Graph request payload instead of sending it.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    /// <summary>
    /// Additional message properties to retrieve.
    /// </summary>
    [Parameter]
    public string[]? Property { get; set; }

    /// <summary>
    /// When present, only outputs the messages that would be removed.
    /// </summary>
    [Parameter]
    public SwitchParameter Preview { get; set; }

    /// <summary>
    /// Message identifiers that should not be removed.
    /// </summary>
    [Parameter]
    public string[]? SkipId { get; set; }

    /// <summary>
    /// Sender addresses to exclude from deletion.
    /// </summary>
    [Parameter]
    public string[]? SkipFrom { get; set; }

    /// <summary>
    /// Recipient addresses to exclude from deletion.
    /// </summary>
    [Parameter]
    public string[]? SkipTo { get; set; }

    /// <summary>
    /// Skips messages when the subject contains any of these strings.
    /// </summary>
    [Parameter]
    public string[]? SkipSubjectContains { get; set; }

    /// <summary>
    /// Skip messages that have attachments.
    /// </summary>
    [Parameter]
    public SwitchParameter SkipHasAttachment { get; set; }

    /// <summary>
    /// Attachment file extensions to exclude.
    /// </summary>
    [Parameter]
    public string[]? SkipAttachmentExtension { get; set; }

    /// <summary>
    /// Timeout for Graph requests in seconds.
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
    /// Executes the cmdlet logic based on the provided parameter set.
    /// </summary>
    /// <returns>The asynchronous task representing the operation.</returns>
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
        var dryRun = !ShouldProcess(UserPrincipalName!, "Clearing Graph junk");
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        MicrosoftGraphUtils.MaxConcurrentRequests = MaxConcurrentRequests;
        if (dryRun) {
            await MicrosoftGraphUtils.ClearJunkMailAsync(
                cred,
                UserPrincipalName!,
                true,
                SkipId,
                SkipFrom,
                SkipTo,
                SkipSubjectContains,
                SkipHasAttachment.IsPresent,
                SkipAttachmentExtension);
            return;
        }
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                await MicrosoftGraphUtils.ClearJunkMailAsync(
                    cred,
                    UserPrincipalName!,
                    false,
                    SkipId,
                    SkipFrom,
                    SkipTo,
                    SkipSubjectContains,
                    SkipHasAttachment.IsPresent,
                    SkipAttachmentExtension);
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
        if ((SkipHasAttachment.IsPresent || SkipAttachmentExtension != null) && !props.Contains("hasAttachments")) props.Add("hasAttachments");

        var messages = await MicrosoftGraphUtils.GetJunkMailMessagesAsync(
            cred,
            UserPrincipalName!,
            props,
            SkipId,
            SkipFrom,
            SkipTo,
            SkipSubjectContains,
            SkipHasAttachment.IsPresent,
            SkipAttachmentExtension);

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
        if (SkipHasAttachment.IsPresent || SkipAttachmentExtension != null) select.Add("hasAttachments");
        var listUri = MicrosoftGraphUtils.JoinUriQuery(
            GraphEndpoint.V1,
            MicrosoftGraphUtils.BuildGraphPath("users", UserPrincipalName!, "mailFolders", "junkemail", "messages"),
            new Dictionary<string, object> { ["$select"] = string.Join(",", select) });
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "GET")
            .AddParameter("Uri", listUri);
        var results = ps.Invoke();
        var dict = results
            .OfType<PSObject>()
            .Select(o => o.Properties.ToDictionary(p => p.Name, p => p.Value))
            .ToList();
        var filtered = MicrosoftGraphUtils.FilterJunkMessages(dict, SkipId, SkipFrom, SkipTo, SkipSubjectContains, SkipHasAttachment.IsPresent);
        if (SkipAttachmentExtension != null && SkipAttachmentExtension.Length > 0) {
            var final = new List<Dictionary<string, object>>();
            foreach (var msg in filtered) {
                if (!msg.TryGetValue("id", out var idObj) || idObj is not string id) continue;
                if (msg.TryGetValue("hasAttachments", out var hasObj) && hasObj is bool ha && ha) {
                    var attPs = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
                    attPs.AddCommand("Invoke-MgGraphRequest")
                        .AddParameter("Method", "GET")
                        .AddParameter(
                            "Uri",
                            MicrosoftGraphUtils.JoinUriQuery(
                                GraphEndpoint.V1,
                                MicrosoftGraphUtils.BuildGraphPath("users", UserPrincipalName!, "messages", id, "attachments"),
                                new Dictionary<string, object> { ["$select"] = "name" }));
                    var attRes = attPs.Invoke();
                    var names = attRes
                        .OfType<PSObject>()
                        .SelectMany<PSObject, PSObject>(o =>
                            ((System.Collections.IEnumerable?)o.Properties["value"].Value)?.OfType<PSObject>() ?? System.Array.Empty<PSObject>())
                        .Select(a => a.Properties["name"].Value as string)
                        .Where(n => n != null)
                        .ToList();
                    if (names.Any(n => SkipAttachmentExtension.Contains(System.IO.Path.GetExtension(n!).TrimStart('.'), StringComparer.OrdinalIgnoreCase))) {
                        continue;
                    }
                }
                final.Add(msg);
            }
            filtered = final;
        }
        foreach (var msg in filtered) {
            var id = msg["id"] as string;
            if (string.IsNullOrWhiteSpace(id)) continue;
            var delPs = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
            delPs.AddCommand("Invoke-MgGraphRequest")
                .AddParameter("Method", "DELETE")
                .AddParameter(
                    "Uri",
                    MicrosoftGraphUtils.BuildGraphUri(
                        GraphEndpoint.V1,
                        MicrosoftGraphUtils.BuildGraphPath("users", UserPrincipalName!, "messages", id!)))
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
        if ((SkipHasAttachment.IsPresent || SkipAttachmentExtension != null) && !props.Contains("hasAttachments")) props.Add("hasAttachments");
        var listUri = MicrosoftGraphUtils.JoinUriQuery(
            GraphEndpoint.V1,
            MicrosoftGraphUtils.BuildGraphPath("users", UserPrincipalName!, "mailFolders", "junkemail", "messages"),
            new Dictionary<string, object> { ["$select"] = string.Join(",", props) });
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "GET")
            .AddParameter("Uri", listUri);
        var results = ps.Invoke();
        var dict = results
            .OfType<PSObject>()
            .Select(o => o.Properties.ToDictionary(p => p.Name, p => p.Value))
            .ToList();
        var filtered = MicrosoftGraphUtils.FilterJunkMessages(dict, SkipId, SkipFrom, SkipTo, SkipSubjectContains, SkipHasAttachment.IsPresent);
        if (SkipAttachmentExtension != null && SkipAttachmentExtension.Length > 0) {
            var final = new List<Dictionary<string, object>>();
            foreach (var msg in filtered) {
                if (!msg.TryGetValue("id", out var idObj) || idObj is not string id) continue;
                if (msg.TryGetValue("hasAttachments", out var hasObj) && hasObj is bool ha && ha) {
                    var attPs = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
                    attPs.AddCommand("Invoke-MgGraphRequest")
                        .AddParameter("Method", "GET")
                        .AddParameter(
                            "Uri",
                            MicrosoftGraphUtils.JoinUriQuery(
                                GraphEndpoint.V1,
                                MicrosoftGraphUtils.BuildGraphPath("users", UserPrincipalName!, "messages", id, "attachments"),
                                new Dictionary<string, object> { ["$select"] = "name" }));
                    var attRes = attPs.Invoke();
                    var names = attRes
                        .OfType<PSObject>()
                        .SelectMany<PSObject, PSObject>(o =>
                            ((System.Collections.IEnumerable?)o.Properties["value"].Value)?.OfType<PSObject>() ?? System.Array.Empty<PSObject>())
                        .Select(a => a.Properties["name"].Value as string)
                        .Where(n => n != null)
                        .ToList();
                    if (names.Any(n => SkipAttachmentExtension.Contains(System.IO.Path.GetExtension(n!).TrimStart('.'), StringComparer.OrdinalIgnoreCase))) {
                        continue;
                    }
                }
                final.Add(msg);
            }
            filtered = final;
        }
        foreach (var msg in filtered) {
            WriteObject(PSObject.AsPSObject(msg));
        }
    }
}
