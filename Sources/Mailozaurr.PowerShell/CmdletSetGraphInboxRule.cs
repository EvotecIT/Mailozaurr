using Mailozaurr;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Updates an existing inbox rule via Microsoft Graph.
/// </summary>
[Cmdlet(VerbsCommon.Set, "GraphInboxRule", SupportsShouldProcess = true)]
[OutputType(typeof(GraphInboxRule))]
public sealed class CmdletSetGraphInboxRule : AsyncPSCmdlet {
    /// <summary>
    /// User principal name owning the rule.
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// Identifier of the rule to update.
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? RuleId { get; set; }

    /// <summary>
    /// Hashtable representing the rule properties.
    /// </summary>
    [Parameter(ParameterSetName = "Graph")]
    [ValidateNotNull]
    public Hashtable? Rule { get; set; }

    /// <summary>
    /// Existing rule object used for update.
    /// </summary>
    [Parameter(ParameterSetName = "Graph")]
    [ValidateNotNull]
    public GraphInboxRule? RuleObject { get; set; }

    /// <summary>
    /// Graph connection context used for the update.
    /// </summary>
    [Parameter(ParameterSetName = "Graph", ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    /// <summary>
    /// When set, uses <c>Invoke-MgGraphRequest</c> for the update instead of the SDK.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    /// <summary>
    /// Timeout in seconds for Graph operations.
    /// </summary>
    [Parameter]
    public int TimeoutSeconds { get; set; } = 100;

    /// <summary>
    /// Number of retry attempts when a request fails.
    /// </summary>
    [Parameter]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Delay between retry attempts in milliseconds.
    /// </summary>
    [Parameter]
    public int RetryDelayMilliseconds { get; set; } = 0;

    /// <summary>
    /// Executes the cmdlet logic asynchronously.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        if (ParameterSetName == "MgGraphRequest") {
            ProcessMgGraph();
            return Task.CompletedTask;
        }

        var conn = Connection ?? DefaultSessions.GraphSession;
        if (conn == null) {
            WriteWarning("Set-GraphInboxRule - Connection not provided and no default session available.");
            return Task.CompletedTask;
        }

        return ProcessGraphAsync(conn.Credential);
    }

    private async Task ProcessGraphAsync(GraphCredential cred) {
        var dryRun = !ShouldProcess(RuleId!, "Updating inbox rule");
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        int attempts = 0;
        Exception? lastException = null;
        var rulePayload = Rule ?? throw new PSArgumentNullException(nameof(Rule), "Rule has to be provided or built.");
        var obj = RuleObject ?? JsonSerializer.Deserialize(JsonSerializer.Serialize(rulePayload, MailozaurrJsonContext.Default.Object), MailozaurrJsonContext.Default.GraphInboxRule);
        if (obj is null) {
            throw new PSArgumentException("Graph inbox rule definition cannot be null.");
        }
        if (dryRun) {
            await MicrosoftGraphUtils.UpdateRuleAsync(cred, UserPrincipalName!, RuleId!, obj, dryRun: true);
            return;
        }
        do {
            try {
                var res = await MicrosoftGraphUtils.UpdateRuleAsync(cred, UserPrincipalName!, RuleId!, obj, dryRun: false);
                WriteObject(res);
                return;
            } catch (Exception ex) {
                lastException = ex;
                WriteWarning($"Set-GraphInboxRule - {ex.Message}");
                if (!Helpers.IsTransient(ex) || attempts >= RetryCount) {
                    if (ex is GraphApiException gex) {
                        WriteError(new ErrorRecord(gex, "GraphApiError", ErrorCategory.InvalidOperation, null));
                    } else {
                        WriteError(new ErrorRecord(ex, "GraphError", ErrorCategory.InvalidOperation, null));
                    }
                    return;
                }
                if (RetryDelayMilliseconds > 0) await Task.Delay(RetryDelayMilliseconds);
            }
            attempts++;
        } while (attempts <= RetryCount);
        if (lastException is not null) {
            WriteError(new ErrorRecord(lastException, "GraphError", ErrorCategory.InvalidOperation, null));
        }
    }

    private void ProcessMgGraph() {
        if (!ShouldProcess(RuleId!, "Updating inbox rule")) {
            return;
        }
        var uri = MicrosoftGraphUtils.JoinUriQuery(
            GraphEndpoint.V1,
            $"/users/{UserPrincipalName}/mailFolders/inbox/messageRules/{RuleId}");
        var rulePayload = Rule ?? throw new PSArgumentNullException(nameof(Rule), "Rule has to be provided or built.");
        var bodyObj = RuleObject ?? JsonSerializer.Deserialize(JsonSerializer.Serialize(rulePayload, MailozaurrJsonContext.Default.Object), MailozaurrJsonContext.Default.GraphInboxRule);
        if (bodyObj is null) {
            throw new PSArgumentException("Graph inbox rule definition cannot be null.");
        }
        if (bodyObj is null) {
            throw new PSArgumentException("Graph inbox rule definition cannot be null.");
        }
        var body = JsonSerializer.Serialize(bodyObj, MailozaurrJsonContext.Default.GraphInboxRule);
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "PATCH")
            .AddParameter("Uri", uri)
            .AddParameter("Body", body)
            .AddParameter("ContentType", "application/json");
        var results = ps.Invoke();
        foreach (var res in results) WriteObject(res);
    }
}