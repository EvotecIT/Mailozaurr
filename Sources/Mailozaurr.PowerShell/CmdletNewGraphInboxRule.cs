using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Mailozaurr;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Creates a new inbox rule via Microsoft Graph.
/// </summary>
[Cmdlet(VerbsCommon.New, "GraphInboxRule")]
[OutputType(typeof(GraphInboxRule))]
public sealed class CmdletNewGraphInboxRule : AsyncPSCmdlet {
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    [Parameter(ParameterSetName = "Graph")]
    [ValidateNotNull]
    public Hashtable? Rule { get; set; }

    [Parameter(ParameterSetName = "Graph")]
    [ValidateNotNull]
    public GraphInboxRule? RuleObject { get; set; }

    [Parameter(ParameterSetName = "Graph")]
    [ValidateNotNull]
    public GraphInboxRuleBuilder? RuleBuilder { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Params")]
    public string DisplayName { get; set; } = string.Empty;

    [Parameter(ParameterSetName = "Params")]
    public int Sequence { get; set; }

    [Parameter(ParameterSetName = "Params")]
    public SwitchParameter Enabled { get; set; }

    [Parameter(ParameterSetName = "Params")]
    public string? MoveToFolder { get; set; }

    [Parameter(ParameterSetName = "Params")]
    public string? CopyToFolder { get; set; }

    [Parameter(ParameterSetName = "Params")]
    public SwitchParameter Delete { get; set; }

    [Parameter(ParameterSetName = "Params")]
    public string[]? ForwardTo { get; set; }

    [Parameter(ParameterSetName = "Params")]
    public SwitchParameter StopProcessing { get; set; }

    [Parameter(ParameterSetName = "Params")]
    public string[]? SenderContains { get; set; }

    [Parameter(ParameterSetName = "Params")]
    public string[]? RecipientContains { get; set; }

    [Parameter(ParameterSetName = "Params")]
    public string[]? SubjectContains { get; set; }

    [Parameter(ParameterSetName = "Params")]
    public string[]? BodyContains { get; set; }

    [Parameter(ParameterSetName = "Params")]
    public string? Importance { get; set; }

    [Parameter(ParameterSetName = "Graph", ValueFromPipeline = true)]
    [Parameter(ParameterSetName = "Params", ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    [Parameter]
    public int TimeoutSeconds { get; set; } = 100;

    [Parameter]
    public int RetryCount { get; set; } = 0;

    [Parameter]
    public int RetryDelayMilliseconds { get; set; } = 0;

    protected override Task ProcessRecordAsync() {
        if (ParameterSetName == "MgGraphRequest") {
            ProcessMgGraph();
            return Task.CompletedTask;
        }

        var conn = Connection ?? DefaultSessions.GraphSession;
        if (conn == null) {
            WriteWarning("New-GraphInboxRule - Connection not provided and no default session available.");
            return Task.CompletedTask;
        }

        return ProcessGraphAsync(conn.Credential);
    }

    private async Task ProcessGraphAsync(GraphCredential cred) {
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        int attempts = 0;
        Exception? lastException = null;
        GraphInboxRule obj;
        if (ParameterSetName == "Params") {
            obj = BuildFromParams();
        } else if (RuleBuilder != null) {
            obj = RuleBuilder.Build();
        } else if (RuleObject != null) {
            obj = RuleObject;
        } else {
            var dict = Rule!.Cast<DictionaryEntry>().ToDictionary(d => (string)d.Key, d => d.Value!);
            var json = JsonSerializer.Serialize(dict);
            obj = JsonSerializer.Deserialize<GraphInboxRule>(json)!;
        }
        do {
            try {
                var res = await MicrosoftGraphUtils.NewRuleAsync(cred, UserPrincipalName!, obj);
                WriteObject(res);
                return;
            } catch (Exception ex) {
                lastException = ex;
                WriteWarning($"New-GraphInboxRule - {ex.Message}");
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
        var uri = MicrosoftGraphUtils.JoinUriQuery(
            "https://graph.microsoft.com/v1.0",
            $"/users/{UserPrincipalName}/mailFolders/inbox/messageRules");
        var bodyObj = RuleBuilder != null
            ? RuleBuilder.Build()
            : RuleObject ?? JsonSerializer.Deserialize<GraphInboxRule>(JsonSerializer.Serialize(Rule));
        var body = JsonSerializer.Serialize(bodyObj, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "POST")
            .AddParameter("Uri", uri)
            .AddParameter("Body", body)
            .AddParameter("ContentType", "application/json");
        var results = ps.Invoke();
        foreach (var res in results) WriteObject(res);
    }

    private GraphInboxRule BuildFromParams() {
        var rule = new GraphInboxRule {
            DisplayName = DisplayName,
            Sequence = Sequence,
            IsEnabled = Enabled.IsPresent
        };

        if (SenderContains != null || RecipientContains != null || SubjectContains != null || BodyContains != null || Importance != null) {
            rule.Conditions = new GraphInboxRulePredicates {
                SenderContains = SenderContains != null ? new List<string>(SenderContains) : null,
                RecipientContains = RecipientContains != null ? new List<string>(RecipientContains) : null,
                SubjectContains = SubjectContains != null ? new List<string>(SubjectContains) : null,
                BodyContains = BodyContains != null ? new List<string>(BodyContains) : null,
                Importance = Importance
            };
        }

        if (!string.IsNullOrEmpty(MoveToFolder) || !string.IsNullOrEmpty(CopyToFolder) || Delete.IsPresent || ForwardTo != null || StopProcessing.IsPresent) {
            rule.Actions = new GraphInboxRuleActions {
                MoveToFolder = MoveToFolder,
                CopyToFolder = CopyToFolder,
                Delete = Delete.IsPresent ? true : null,
                ForwardTo = ForwardTo != null ? new List<GraphEmailAddress>(ForwardTo.Select(a => new GraphEmailAddress { Email = new GraphEmail { Address = a } })) : null,
                StopProcessingRules = StopProcessing.IsPresent ? true : null
            };
        }

        return rule;
    }
}
