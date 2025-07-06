using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Creates a <see cref="GraphInboxRule"/> object.
/// </summary>
[Cmdlet(VerbsCommon.New, "GraphInboxRuleObject")]
[OutputType(typeof(GraphInboxRule))]
public sealed class CmdletNewGraphInboxRuleObject : PSCmdlet
{
    [Parameter(Mandatory = true, ParameterSetName = "Params")]
    public string DisplayName { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Builder")]
    public GraphInboxRuleBuilder? Builder { get; set; }

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

    protected override void ProcessRecord()
    {
        if (ParameterSetName == "Builder")
        {
            WriteObject(Builder!.Build());
            return;
        }

        var rule = new GraphInboxRule
        {
            DisplayName = DisplayName,
            Sequence = Sequence,
            IsEnabled = Enabled.IsPresent
        };

        if (SenderContains != null || RecipientContains != null || SubjectContains != null || BodyContains != null || Importance != null)
        {
            rule.Conditions = new GraphInboxRulePredicates
            {
                SenderContains = SenderContains != null ? new List<string>(SenderContains) : null,
                RecipientContains = RecipientContains != null ? new List<string>(RecipientContains) : null,
                SubjectContains = SubjectContains != null ? new List<string>(SubjectContains) : null,
                BodyContains = BodyContains != null ? new List<string>(BodyContains) : null,
                Importance = Importance
            };
        }

        if (!string.IsNullOrEmpty(MoveToFolder) || !string.IsNullOrEmpty(CopyToFolder) || Delete.IsPresent || ForwardTo != null || StopProcessing.IsPresent)
        {
            rule.Actions = new GraphInboxRuleActions
            {
                MoveToFolder = MoveToFolder,
                CopyToFolder = CopyToFolder,
                Delete = Delete.IsPresent ? true : null,
                ForwardTo = ForwardTo != null ? new List<GraphEmailAddress>(ForwardTo.Select(a => new GraphEmailAddress { Email = new GraphEmail { Address = a } })) : null,
                StopProcessingRules = StopProcessing.IsPresent ? true : null
            };
        }

        WriteObject(rule);
    }
}
